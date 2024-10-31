namespace Arbitrum.DataEntities
{
    /// <summary>
    /// Errors originating in Arbitrum SDK
    /// </summary>
    public class ArbSdkError : Exception
    {
        public ArbSdkError(string message, Exception? innerException = null)
            : base(message, innerException)
        {
            if (innerException != null)
            {
                // Append inner exception details to the stack trace
                SetStackTrace($"{Environment.NewLine}Caused By: {innerException.StackTrace}");
            }
        }

        private void SetStackTrace(string stackTrace)
        {
            var remoteStackTraceString = typeof(Exception).GetField("_remoteStackTraceString", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            remoteStackTraceString?.SetValue(this, stackTrace);
        }
    }

    /// <summary>
    /// Thrown when a signer does not have a connected provider
    /// </summary>
    public class MissingProviderArbSdkError : ArbSdkError
    {
        public MissingProviderArbSdkError(string signerName)
            : base($"{signerName} does not have a connected provider and one is required.")
        {
        }
    }
}
