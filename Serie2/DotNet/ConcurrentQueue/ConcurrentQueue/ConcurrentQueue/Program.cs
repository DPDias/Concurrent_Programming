using System.Threading;


namespace ConcurrentQueue
{
    public class ConcurrentQueue<T>
    {
        private class Node<T>
        {
            public readonly T item;
            public volatile Node<T> next;
            public Node(T item, Node<T> next)
            {
                this.item = item;
                this.next = next;
            }
        }

        private readonly Node<T> sentinel;
        private volatile Node<T> head;
        private volatile Node<T> tail;

        public ConcurrentQueue()
        {
            sentinel = new Node<T>(default(T), null);
            sentinel = head = sentinel;
        }

        public void put(T item)
        {
            Node<T> newNode = new Node<T>(item, null);

            while (true)
            {

                Node<T> curTail = tail;
                Node<T> tailNext = curTail.next;

                if (curTail == tail)
                {

                    if (tailNext != null)
                    {
                        Interlocked.CompareExchange(ref tail, tailNext, curTail);              
                    }
                    else
                    {
                        if (Interlocked.CompareExchange(ref curTail.next, newNode, null) == null)
                        {
                           
                            Interlocked.CompareExchange(ref tail, newNode, curTail);
                            return;
                        }
                    }
                }
            }
        }

        public bool isEmpty()
        {
            return head.next == null;
        }

        public T tryTake()
        {
            Node<T> oldHead;
            Node<T> newHead;
            do
            {
                oldHead = head;
                newHead = oldHead.next;
                if (newHead == null)
                    return default(T);
            } while (Interlocked.CompareExchange(ref head, newHead, oldHead) != oldHead);

            return newHead.item;
        }
    }




    class Program
    {
        static void Main(string[] args)
        {
        }
    }
}
