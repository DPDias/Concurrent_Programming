import java.util.LinkedList;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

import static jdk.nashorn.internal.objects.NativeArray.push;

public class SimpleThreadPoolExecutor{

   // private final NodeLinkedList<ThreadContentor> runningThreads = new NodeLinkedList<>();
    private final NodeLinkedList<ThreadContentor> blockedThreads = new NodeLinkedList<>();
    private final NodeLinkedList<Tasks> tasks = new NodeLinkedList<>();
    private final Lock lock = new ReentrantLock();
    private final Condition awaitTerminationThreads;
    private final int maxPoolSize;
    private final int keepAliveTime;
    private int numberOfThreads;
    private boolean shutdown = false;

    public SimpleThreadPoolExecutor(int maxPoolSize, int keepAliveTime){
        numberOfThreads = 0;
        this.maxPoolSize = maxPoolSize;
        this.keepAliveTime = keepAliveTime;
        awaitTerminationThreads = lock.newCondition();
    }

    public boolean execute(Runnable command, int timeout) throws InterruptedException {
        if(shutdown)            //havera diferença por
            throw new RejectedExecutionException();

        lock.lock();
        try {
            if (numberOfThreads < maxPoolSize) {
                numberOfThreads ++;
                ThreadContentor threadContentor = new ThreadContentor(true, lock.newCondition());
                Thread created = new Thread(() -> threadWork(command, threadContentor));     //posso perder referência?
                created.start();
                return true;
            }

            if(!blockedThreads.isEmpty()){
                tasks.push(new Tasks(command, null, true));     //pode ser null?, ponho a lista na fila, ou meto no objecto threadContentor
                ThreadContentor threadContentor = blockedThreads.pull().value;  // para fazer isso tinha que por um boolean para verificar se foi lhe dado trabalho
                threadContentor.condition.signal();
                threadContentor.running = true;         //para sinalizar que acordou
                return true;
            }

            Condition condition = lock.newCondition();
            NodeLinkedList.Node<Tasks> task = tasks.push(new Tasks(command, condition, false));

            long t = Timeouts.start(timeout);
            long remaining = Timeouts.remaining(t);

            while (true) {
                try {
                    condition.await(remaining, TimeUnit.MILLISECONDS);
                } catch (InterruptedException e) {
                    if(task.value.taked)
                        return true;
                    tasks.remove(task);
                    throw e;
                }

                if (task.value.taked){
                    return true;
                }

                remaining = Timeouts.remaining(t);
                if(Timeouts.isTimeout(remaining)) {
                    return false;
                }
            }
        }finally{
            lock.unlock();
        }
    }

    public void shutdown(){
        shutdown = true;        //não necessita de exclusão
    }

    public boolean awaitTermination(int timeout){
        long t = Timeouts.start(timeout);
        long remaining = Timeouts.remaining(t);

        lock.lock();
        try{
            while(true){
                if(numberOfThreads==0 && shutdown){
                    return true;
                }
                try {
                    awaitTerminationThreads.await(remaining, TimeUnit.MILLISECONDS);
                } catch (InterruptedException e) {

                }

                remaining = Timeouts.remaining(t);
                if(Timeouts.isTimeout(remaining)) {
                    return false;
                }
            }
        }
        finally {
            lock.unlock();
        }
    }

    private void threadWork(Runnable command, ThreadContentor threadContentor){
        command.run();
        NodeLinkedList.Node<ThreadContentor> node = new NodeLinkedList.Node(threadContentor);
        lock.lock();
        try {

            long t = Timeouts.start(keepAliveTime);
            long remaining = Timeouts.remaining(t);

            while (true) {

                if (!tasks.isEmpty()) {

                    node = ifBlockedChangeToRunning(node);

                    NodeLinkedList.Node<Tasks> run = tasks.pull();

                    if(!run.value.taked) {          //uma thread running pode roubar task de um thread blocked
                        run.value.taked = true;
                        run.value.condition.signal();
                    }

                    lock.unlock();
                    run.value.runnable.run();        //poderá dar exceção?

                    t = Timeouts.start(keepAliveTime);
                    remaining = Timeouts.remaining(t);
                    lock.lock();
                }
                else {
                    node = ifRunningChangeToBlocked(node);
                    try {
                        threadContentor.condition.await(remaining, TimeUnit.MILLISECONDS);  //tratar timer
                    } catch (InterruptedException e) {
                                //errado
                    }

                    if (!threadContentor.running) {
                        remaining = Timeouts.remaining(t);          //poderei ter que sinalizar um monitor, para dizer que acabou tudo
                        if(Timeouts.isTimeout(remaining)) {
                            if(--numberOfThreads == 0 && shutdown)
                                awaitTerminationThreads.signalAll();
                            blockedThreads.remove(node);
                            return;
                        }
                    }
                }
            }
        }
        finally{
            lock.unlock();  //poderá não ter o lock???
        }
    }

    private NodeLinkedList.Node<ThreadContentor> ifBlockedChangeToRunning(NodeLinkedList.Node<ThreadContentor> node) {
        if(!node.value.running){
            blockedThreads.remove(node);
            node.value.running = true;
        }
        return node;
    }

    private NodeLinkedList.Node<ThreadContentor> ifRunningChangeToBlocked(NodeLinkedList.Node<ThreadContentor> node) {
        if(node.value.running){
            NodeLinkedList.Node<ThreadContentor> aux = blockedThreads.push(node.value);
            node = aux;
            node.value.running = false;
        }
        return node;
    }

    public static class ThreadContentor{
        public boolean running;
        public final Condition condition;

        public ThreadContentor(boolean running, Condition condition) {
            this.condition = condition;
            this.running = running;
        }
    }

    public static class Tasks{
        public Runnable runnable;
        public final Condition condition;
        public boolean taked;

        public Tasks(Runnable runnable, Condition condition, boolean taked) {
            this.runnable = runnable;
            this.condition = condition;
            this.taked = taked;
        }
    }




}
