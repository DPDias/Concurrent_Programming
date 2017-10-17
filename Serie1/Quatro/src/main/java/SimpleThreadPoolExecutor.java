import java.util.LinkedList;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

import static jdk.nashorn.internal.objects.NativeArray.push;

public class SimpleThreadPoolExecutor{

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
        lock.lock();
        try {

            if(shutdown)
                throw new RejectedExecutionException();

            if(!blockedThreads.isEmpty()){
                tasks.push(new Tasks(command, null, true));
                ThreadContentor threadContentor = blockedThreads.pull().value;
                threadContentor.condition.signal();
                threadContentor.running = true;
                return true;
            }

            if (numberOfThreads < maxPoolSize) {
                numberOfThreads ++;
                ThreadContentor threadContentor = new ThreadContentor(true, lock.newCondition());
                Thread created = new Thread(() -> threadWork(command, threadContentor));
                created.start();
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
        lock.lock();
        shutdown = true;
        while(!blockedThreads.isEmpty()){
            blockedThreads.pull().value.condition.signal();
        }
        lock.unlock();
    }

    public boolean awaitTermination(int timeout) throws InterruptedException {
        long t = Timeouts.start(timeout);
        long remaining = Timeouts.remaining(t);

        lock.lock();
        try{
            while(true){
                if(numberOfThreads==0 && shutdown){
                    return true;
                }

                awaitTerminationThreads.await(remaining, TimeUnit.MILLISECONDS);

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

                    node = ifBlockedChangeToRunning(node);      //se calhar posso remover

                    NodeLinkedList.Node<Tasks> run = tasks.pull();

                    if(!run.value.taked) {
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

                    if(shutdown){
                        if(--numberOfThreads==0)
                            awaitTerminationThreads.signalAll();
                        return;
                    }

                    node = ifRunningChangeToBlocked(node);

                    try {
                        threadContentor.condition.await(remaining, TimeUnit.MILLISECONDS);
                    } catch (InterruptedException e) {
                        if(!threadContentor.running){
                            blockedThreads.remove(node);
                            numberOfThreads--;
                            return;
                        }
                    }

                    if (!threadContentor.running) {
                        remaining = Timeouts.remaining(t);
                        if(Timeouts.isTimeout(remaining)) {
                            --numberOfThreads;
                            blockedThreads.remove(node);
                            return;
                        }
                    }
                }
            }
        }
        finally{
            lock.unlock();
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
        public boolean workGiven;

        public ThreadContentor(boolean running, Condition condition) {
            workGiven = false;
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
