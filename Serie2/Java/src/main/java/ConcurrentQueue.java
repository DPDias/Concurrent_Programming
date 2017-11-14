import java.util.concurrent.atomic.AtomicReference;

public class ConcurrentQueue<T> {
    private static class Node <T> {
        public T item;
        public final AtomicReference<Node<T>> next;
        public Node(T item, Node<T> next) {
            this.item = item;
            this.next = new AtomicReference<Node<T>>(next);
        }
    }

    private final Node<T> sentinel = new Node<T>(null, null);
    private final AtomicReference<Node<T>> head = new AtomicReference<Node<T>>(sentinel);
    private final AtomicReference<Node<T>> tail = new AtomicReference<Node<T>>(sentinel);

    public void put(T item) {
        Node<T> newNode = new Node<T>(item, null);

        while (true) {

            Node<T> curTail = tail.get();
            Node<T> tailNext = curTail.next.get();

            if (curTail == tail.get()) {

                if (tailNext != null) {
                    tail.compareAndSet(curTail, tailNext);

                } else {
                    if (curTail.next.compareAndSet(null, newNode)) {
                        tail.compareAndSet(curTail, newNode);
                        return  ;
                    }
                }
            }
        }
    }

    public boolean isEmpty(){
        return head.get().next.get() == null;
    }

    public T tryTake(){
        Node <T> oldHead;
        Node <T> newHead;
        do{
            oldHead = head.get();
            newHead = oldHead.next.get();
            if(newHead == null)
                return null;
        }while(!head.compareAndSet(oldHead, newHead));
        T value = newHead.item;
        newHead.item =  null;
        return value;
    }
}
