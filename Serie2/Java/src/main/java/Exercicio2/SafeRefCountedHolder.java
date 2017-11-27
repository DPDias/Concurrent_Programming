package Exercicio2;

import java.util.concurrent.atomic.AtomicInteger;

public class SafeRefCountedHolder<T> {
    private T value;
    private AtomicInteger refCount;

    public SafeRefCountedHolder(T value){
        this.value = value;
        refCount = new AtomicInteger(1);
    }

    public void addRef(){
        while(true){
            int count = refCount.get();
            if(count == 0)
                throw new IllegalStateException();
            if(refCount.compareAndSet(count, count + 1))
                return;
        }
    }

    public void ReleaseRef(){
        int count;
        while(true) {
            count = refCount.get();
            if (count == 0)
                throw new IllegalStateException();      // ver se pode ser esta exceção
            if (refCount.compareAndSet(count, count - 1))
                break;
        }
        if(count == 1){
            IDisposable disposable = (IDisposable)value;        //que classe uso? Posso criar uma interface
            value = null;
            if(disposable != null)
                disposable.dispose();
        }
    }

    public T getValue(){
        if(refCount.get() == 0)
            throw new IllegalStateException();
        return value;
    }
}
