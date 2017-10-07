public class SimpleThreadPoolExecutor{

    private final NodeLinkedList<Thread> threadsList = new NodeLinkedList<Thread>();
    private final int maxPoolSize;
    private final int keepAliveTime;
    private int numberOfThreads;

    public SimpleThreadPoolExecutor(int maxPoolSize, int keepAliveTime){
        numberOfThreads = 0;
        this.maxPoolSize = maxPoolSize;
        this.keepAliveTime = keepAliveTime;
    }

    public boolean execute(Runnable command, int timeout) throws InterruptedException{
        if(numberOfThreads < maxPoolSize){
            Thread run = new Thread(command);
            threadsList.push(run);

        }

        return false;
    }


}
