package Exercicio2;

import java.util.concurrent.atomic.AtomicReference;

public class LockFreeDualQueue<T> {

    // types of queue nodes
    private enum NodeType { DATUM, REQUEST };

    // the queue node
    private static class QNode<T> {
        NodeType type;
        final T data;
        final AtomicReference<QNode<T>> request;
        final AtomicReference<QNode<T>> next;

        //  build a datum or request node
        QNode(T d, NodeType t) {
            type = t;
            data = d;
            request = new AtomicReference<QNode<T>>(null);
            next = new AtomicReference<QNode<T>>(null);
        }
    }

    // the head and tail references
    private final AtomicReference<QNode<T>> head;
    private final AtomicReference<QNode<T>> tail;

    public LockFreeDualQueue() {
        QNode<T> sentinel = new QNode<T>(null, NodeType.DATUM);
        head = new AtomicReference<QNode<T>>(sentinel);
        tail = new AtomicReference<QNode<T>>(sentinel);
    }

    // enqueue a datum
    public void enqueue(T v) {
        QNode<T> node = new QNode<>(v, NodeType.DATUM);
        while(true){
            QNode<T> obsTail = tail.get();
            QNode<T> obsHead = head.get();
            if(obsHead == obsTail || obsTail.type == NodeType.DATUM){
                QNode<T> next = obsTail.next.get();
                if(obsTail == tail.get()){
                    if(next != null){
                        tail.compareAndSet(obsTail, next);
                    }
                    else {
                        if(obsTail.next.compareAndSet(next, node)){
                            tail.compareAndSet(obsTail, node);
                            return;
                        }
                    }
                }
            }
            else{
                QNode<T> next = obsHead.next.get();
                if(obsTail == tail.get()){
                    QNode req = obsHead.request.get();
                    if(obsHead == head.get()){
                        boolean success = (req == null && obsHead.request.compareAndSet(req, node));
                        head.compareAndSet(obsHead, next);
                        if(success)
                            return;
                    }
                }
            }

        }

    }

    // dequeue a datum - spinning if necessary
    public T dequeue() throws InterruptedException {
        QNode<T> h, hnext, t, tnext, n = null;
        do {
            h = head.get();
            t = tail.get();

            if (t == h || t.type == NodeType.REQUEST) {
                // queue empty, tail falling behind, or queue contains data (queue could also
                // contain exactly one outstanding request with tail pointer as yet unswung)
                tnext = t.next.get();

                if (t == tail.get()) {		// tail and next are consistent
                    if (tnext != null) {	// tail falling behind
                        tail.compareAndSet(t, tnext);
                    } else {	// try to link in a request for data
                        if (n == null) {
                            n = new QNode<T>(null, NodeType.REQUEST);
                        }
                        if (t.next.compareAndSet(null, n)) {
                            // linked in request; now try to swing tail pointer
                            tail.compareAndSet(t, n);

                            // help someone else if I need to
                            if (h == head.get() && h.request.get() != null) {
                                head.compareAndSet(h, h.next.get());
                            }

                            // busy waiting for a data done.
                            // we use sleep instead od yield in order to accept interrupts
                            while (t.request.get() == null) {
                                Thread.sleep(0);  // spin accepting interrupts!!!
                            }

                            // help snip my node
                            h = head.get();
                            if (h == t) {
                                head.compareAndSet(h, n);
                            }

                            // data is now available; read it out and go home
                            return t.request.get().data;
                        }
                    }
                }
            } else {    // queue consists of real data
                hnext = h.next.get();
                if (t == tail.get()) {
                    // head and next are consistent; read result *before* swinging head
                    T result = hnext.data;
                    if (head.compareAndSet(h, hnext)) {
                        return result;
                    }
                }
            }
        } while (true);
    }

    public boolean isEmpty() {
        return head.get() == tail.get() || tail.get().type == NodeType.REQUEST;
    }

}
