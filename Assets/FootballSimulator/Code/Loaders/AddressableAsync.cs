using System;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace FStudio.Loaders {
    public static class AddressableAsync {
        /// <summary>
        /// Wait on Addressables' main-thread completion callback, including on WebGL.
        /// Successful handles keep their existing owner; failed handles are released.
        /// </summary>
        public static Task<T> AwaitResult<T>(this AsyncOperationHandle<T> operation) {
            if (!operation.IsValid()) {
                return Task.FromException<T>(new InvalidOperationException("Invalid Addressables operation."));
            }

            // Do not queue continuations to the thread pool: WebGL runs on the main thread.
            var completion = new TaskCompletionSource<T>();

            void OnCompleted(AsyncOperationHandle<T> completed) {
                completed.Completed -= OnCompleted;

                if (completed.Status == AsyncOperationStatus.Succeeded) {
                    completion.TrySetResult(completed.Result);
                } else {
                    var exception = completed.OperationException ??
                        new InvalidOperationException("Addressables failed to load the requested asset.");
                    Addressables.Release(completed);
                    completion.TrySetException(exception);
                }
            }

            if (operation.IsDone) {
                OnCompleted(operation);
            } else {
                operation.Completed += OnCompleted;
            }

            return completion.Task;
        }
    }
}
