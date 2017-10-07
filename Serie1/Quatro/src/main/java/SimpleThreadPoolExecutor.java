import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

public class SimpleThreadPoolExecutor{

    private final NodeLinkedList<ThreadContentor> threadsList = new NodeLinkedList<>();
    private final NodeLinkedList<Runnable> tasks = new NodeLinkedList<>();
    private final Lock lock = new ReentrantLock();
    private final int maxPoolSize;
    private final int keepAliveTime;
    private int numberOfThreads;

    public SimpleThreadPoolExecutor(int maxPoolSize, int keepAliveTime){
        numberOfThreads = 0;
        this.maxPoolSize = maxPoolSize;
        this.keepAliveTime = keepAliveTime;
    }

    public boolean execute(Runnable command, int timeout) throws InterruptedException{
        lock.lock();
        try {

            long t = Timeouts.start(timeout);
            long remaining = Timeouts.remaining(t);

            if (numberOfThreads < maxPoolSize) {
                NodeLinkedList.Node<ThreadContentor> node = threadsList.push(new ThreadContentor(null, true, lock.newCondition()));
                Thread run = new Thread(() -> threadWork(command, node));
                node.value.thread = run;
                return true;
            }

            while (true) {


                //Check if there is one thread blocked but ready to work
                //two lists or search in this list?????

                Condition condition = lock.newCondition();
                condition.await(remaining, TimeUnit.MILLISECONDS);  //lanço a exceção

                remaining = Timeouts.remaining(t);
                if(Timeouts.isTimeout(remaining)) {
                    return false;
                }
            }
        }finally{
            lock.unlock();
        }
    }

    public void threadWork(Runnable command, NodeLinkedList.Node<ThreadContentor> node){
        command.run();
        boolean timeout = false;
        lock.lock();
        try {
            while (true) {

                if (!tasks.isEmpty() && !timeout) {
                    NodeLinkedList.Node<Runnable> run = tasks.pull();
                    lock.unlock();
                    timeout = true;
                    run.value.run();        //poderá dar exceção?
                    lock.lock();
                }
                else {
                    try {
                        node.value.condition.await(keepAliveTime, TimeUnit.MILLISECONDS);  //tratar timer
                    } catch (InterruptedException e) {
                        timeout = false;
                    }
                    if (timeout) {
                        //tempo

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
        public boolean running;
        public final Condition condition;

        public ThreadContentor(Thread thread, boolean running, Condition condition) {
            this.thread = thread;
            this.condition = condition;
            this.running = running;
        }
    }



}
