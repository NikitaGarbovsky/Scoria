namespace Scoria.Services
{
    /// <summary>
    /// Abstraction for showing small, transient “toast” notifications
    /// (for example, *Saved: MyNote.md*).  
    /// Makes the UI layer independent of the concrete
    /// implementation (<see cref="ToastService"/>).
    /// </summary>
    public interface IToastService
    {
        /// <summary>
        /// Display a toast whose body text is <paramref name="_message"/>.
        /// Implementations decide the exact look &amp; lifetime.
        /// </summary>
        void Show(string _message);
    }    
}
