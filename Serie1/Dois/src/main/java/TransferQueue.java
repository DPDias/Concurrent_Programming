import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

public class TransferQueue <T> {
    private final Lock lock = new ReentrantLock();
    private final NodeLinkedList<Message<T>> saveMsg = new NodeLinkedList<>();
    private final NodeLinkedList<TakeMessage<T>> takeList = new NodeLinkedList<>();

    public void put(T msg) {
        lock.lock();
        try {
            if (checkAndTransferToTake(msg)) return;

            Condition condition = lock.newCondition();
            saveMsg.push(new Message<>(false, condition, msg));
        }
        finally {
            lock.unlock();
        }
    }

    public boolean transfer(T msg, int timeout) throws InterruptedException {
        lock.lock();
        try{
            if (checkAndTransferToTake(msg)) return true;

            if(Timeouts.noWait(timeout)) {
                return false;
            }

            Condition condition = lock.newCondition();
            NodeLinkedList.Node<Message<T>> node = saveMsg.push(new Message<>(false, condition, msg));

            long t = Timeouts.start(timeout);
            long remaining = Timeouts.remaining(t);

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

    private boolean checkAndTransferToTake(T msg) {
        if(!takeList.isEmpty()){
            NodeLinkedList.Node<TakeMessage<T>> node =  takeList.pull();
            node.value.taked = true;
            node.value.msg = msg;
            node.value.condition.signal();
            return true;
        }
        return false;
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

            if (Timeouts.noWait(timeout))
                return false;

            Condition condition = lock.newCondition();
            NodeLinkedList.Node<TakeMessage<T>> node = takeList.push(new TakeMessage<>(false, condition));

            long t = Timeouts.start(timeout);
            long remaining = Timeouts.remaining(t);

            while (true) {
                try {
                    condition.await(remaining, TimeUnit.MILLISECONDS);
                }catch(InterruptedException e){
                    if(node.value.taked) {
                        Thread.currentThread().interrupt();
                        return true;
                    }
                    takeList.remove(node);
                    throw e;
                }

                if (node.value.taked ) {
                    rmsg[0] = node.value.msg;
                    return true;
                }

                remaining = Timeouts.remaining(t);
                if (Timeouts.isTimeout(remaining)) {
                    takeList.remove(node);
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

    public static class TakeMessage<T>{
        public boolean taked;
        public final Condition condition;
        public T msg;

        public TakeMessage(boolean taked, Condition condition) {
            this.taked = taked;
            this.condition = condition;
        }
    }
}
