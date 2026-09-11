using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace Naufal_Windows_Tech_s_Powertoys
{
    /// <summary>
    /// Bounds WinRT package deployment operations. Windows can otherwise leave
    /// an AppX operation pending indefinitely when a package is in use or the
    /// deployment stack is unhealthy, which would stall every later catalog
    /// item behind it.
    /// </summary>
    internal static class DeploymentOperationTimeout
    {
        private static readonly BoundedOperationGate Gate = new();

        // An external Appx cmdlet cannot reliably cancel its Windows deployment.
        // Keep the shared gate until the actual process exits; never kill the
        // client and claim that the OS reset stopped. A timeout aborts this repair.
        internal static Task<T> AwaitExternalAsync<T>(Func<Task<T>> start, string operationName) =>
            Gate.RunAsync(start, () => { }, () => { }, operationName,
                TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(5));

        public static Task<DeploymentResult> AwaitAsync(
            Func<IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress>> start,
            string operationName,
            TimeSpan? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

            IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress>? operation = null;
            var reporter = CatalogOperationProgress.Current;
            return Gate.RunAsync(() =>
            {
                operation = start();
                reporter?.Report(new(operationName, null));
                return operation.AsTask(new InlineProgress<DeploymentProgress>(progress =>
                    reporter?.Report(new(operationName, progress.percentage))));
            }, () => operation?.Cancel(), () => operation?.Close(), operationName,
                timeout ?? TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(5));
        }

        private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
        {
            public void Report(T value) => report(value);
        }
    }
}
