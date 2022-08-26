using System;
using System.Threading;

namespace TopMostFriend {
    public class ActionTimeout {
        private readonly Action Action;
        private bool Continue = true;
        private int Remaining = 0;
        private const int STEP = 500;

        public ActionTimeout(Action action, int timeout) {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            if(timeout < 1)
                throw new ArgumentException(@"Timeout must be a positive integer.", nameof(timeout));
            Remaining = timeout;
            new Thread(ThreadBody) { IsBackground = true }.Start();
        }

        private void ThreadBody() {
            do {
                Thread.Sleep(STEP);
                Remaining -= STEP;

                if(!Continue)
                    return;
            } while(Remaining > 0);

            Action.Invoke();
        }

        public void Cancel() {
            Continue = false;
        }
    }
}
