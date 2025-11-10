namespace P2PShare.Libs
{
    public class Cancellation
    {
        private CancellationTokenSource? _tokenSource;
        private int _timeout;
        public CancellationTokenSource? TokenSource
        {
            get
            {
                return _tokenSource;
            }
        }

        public Cancellation()
        {
            _timeout = 120000; // 2 min
        }

        public void Cancel()
        {
            TokenSource?.Cancel();
            TokenSource?.Dispose();
            _tokenSource = null;
        }

        public async Task TimeOut()
        {
            await Task.Delay(_timeout);

            Cancel();
        }

        public void NewTokenSource()
        {
            _tokenSource = new();
        }
    }
}
