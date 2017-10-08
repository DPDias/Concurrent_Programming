import java.util.LinkedList;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

import static jdk.nashorn.internal.objects.NativeArray.push;

public class SimpleThreadPoolExecutor{

    private final NodeLinkedList<ThreadContentor> runningThreads = new NodeLinkedList<>();
    private final NodeLinkedList<ThreadContentor> blockedThreads = new NodeLinkedList<>();
    private final NodeLinkedList<Tasks> tasks = new NodeLinkedList<>();
    private final Lock lock = new ReentrantLock();
    private final int maxPoolSize;
    private final int keepAliveTime;
    private int numberOfThreads;

    public SimpleThreadPoolExecutor(int maxPoolSize, int keepAliveTime){
        numberOfThreads = 0;
        this.maxPoolSize = maxPoolSize;
        this.keepAliveTime = keepAliveTime;
    }

    public boolean execute(Runnable command, int timeout) throws InterruptedException {
        lock.lock();
        try {

            if (numberOfThreads < maxPoolSize) {
                NodeLinkedList.Node<ThreadContentor> node = runningThreads.push(new ThreadContentor(null, true, lock.newCondition()));
                Thread run = new Thread(() -> threadWork(command, node));
                node.value.thread = run;
                return true;
            }

            if(!blockedThreads.isEmpty()){      //poderei por fora do while
                tasks.push(new Tasks(command, null, true));
                NodeLinkedList.Node<ThreadContentor> aux = runningThreads.push(blockedThreads.pull().value);
                aux.value.condition.signal();
                return true;
            }

            Condition condition = lock.newCondition();
            NodeLinkedList.Node<Tasks> task = tasks.push(new Tasks(command, condition, false));

            long t = Timeouts.start(timeout);
            long remaining = Timeouts.remaining(t);

            while (true) {

                try {
                    condition.await(remaining, TimeUnit.MILLISECONDS);  //lanço a exceção
                } catch (InterruptedException e) {
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

    private void threadWork(Runnable command, NodeLinkedList.Node<ThreadContentor> node){
        command.run();

        lock.lock();
        try {

            long t = Timeouts.start(keepAliveTime);
            long remaining = Timeouts.remaining(t);

            while (true) {

                if (!tasks.isEmpty()) {

                    if(!node.value.running){  //método possivelmente
                        NodeLinkedList.Node<ThreadContentor> aux = runningThreads.push(node.value);
                        blockedThreads.remove(node);
                        node = aux;
                        node.value.running = true;
                    }

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
                    if(node.value.running){     //método possivelmente
                        NodeLinkedList.Node<ThreadContentor> aux = blockedThreads.push(node.value);
                        runningThreads.remove(node);
                        node = aux;
                        node.value.running = false;
                    }
                    try {
                        node.value.condition.await(remaining, TimeUnit.MILLISECONDS);  //tratar timer
                    } catch (InterruptedException e) {
                                //errado
                    }

                    if (!node.value.running) {
                        remaining = Timeouts.remaining(t);
                        if(Timeouts.isTimeout(remaining)) {
                            numberOfThreads--;
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

    public static class ThreadContentor{
        public Thread thread;
        public boolean running;     //devo poder retirar
        public final Condition condition;

        public ThreadContentor(Thread thread, boolean running, Condition condition) {
            this.thread = thread;
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
