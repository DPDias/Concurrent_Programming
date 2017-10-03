import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

public class TransferQueue <T> {
    private final Lock lock = new ReentrantLock();
    private final NodeLinkedList<Message<T>> saveMsg = new NodeLinkedList<>();
    private final Condition takeWait;

    public TransferQueue() {
        this.takeWait = lock.newCondition();
    }

    public void put(T msg) {
        Thread t = new Thread(() -> {
            try {
                transfer(msg, -1);
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
        }); // Infinito = -1 ??
        t.start();
    }

    public boolean transfer(T msg, int timeout) throws InterruptedException {
        lock.lock();
        try{
            if(Timeouts.noWait(timeout)) {
                return false;
            }

            long t = Timeouts.start(timeout);
            long remaining = Timeouts.remaining(t);

            Condition condition = lock.newCondition();
            NodeLinkedList.Node<Message<T>> node = saveMsg.push(new Message<>(false, condition, msg));
            takeWait.signal();

            while(true) {
                try {
                    condition.await(remaining, TimeUnit.MILLISECONDS);
                }catch(InterruptedException e){
                    if(node.value.taked) {
                        Thread.currentThread().interrupt();
                        return true;
                    }
                    saveMsg.remove(node);
                    throw e;
                }

                if(node.value.taked) {
                    return true;
                }

                remaining = Timeouts.remaining(t);
                if(Timeouts.isTimeout(remaining)) {
                    saveMsg.remove(node);
                    return false;
                }
            }
        }finally {
            lock.unlock();
        }
    }

    public boolean take(int timeout, T [] rmsg) throws InterruptedException {
        lock.lock();
        try{

            if (!saveMsg.isEmpty()) {
                NodeLinkedList.Node<Message<T>> aux = saveMsg.pull();
                rmsg[0] = aux.value.msg;
                aux.value.taked = true;
                aux.value.condition.signal();
                return true;
            }

            if (Timeouts.noWait(timeout)) {
                return false;
            }

            long t = Timeouts.start(timeout);
            long remaining = Timeouts.remaining(t);

            while (true) {
                takeWait.await(remaining, TimeUnit.MILLISECONDS);

                if (!saveMsg.isEmpty()) {
                    NodeLinkedList.Node<Message<T>> aux = saveMsg.pull();
                    rmsg[0] = aux.value.msg;
                    aux.value.taked = true;
                    aux.value.condition.signal();
                    return true;
                }

                remaining = Timeouts.remaining(t);
                if (Timeouts.isTimeout(remaining)) {
                    return false;
                }
            }
        }finally {
            lock.unlock();
        }
    }

    public static class Message<T>{
        public boolean taked;
        public final Condition condition;
        public final T msg;

        public Message(boolean taked, Condition condition, T msg) {
            this.taked = taked;
            this.condition = condition;
            this.msg = msg;
        }
    }
}
