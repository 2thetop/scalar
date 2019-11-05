using Scalar.Common.NamedPipes;
using Scalar.Common.Tracing;
using System.Runtime.Serialization;

namespace Scalar.Service.Handlers
{
    /// <summary>
    /// RequestHandler - Routes client requests that reach Scalar.Service to
    /// appropriate MessageHandler object.
    /// </summary>
    public class RequestHandler
    {
        protected string requestDescription;

        private const string UnknownRequestDescription = "unknown";

        private string etwArea;
        private ITracer tracer;

        public RequestHandler(ITracer tracer, string etwArea)
        {
            this.tracer = tracer;
            this.etwArea = etwArea;
        }

        public void HandleRequest(ITracer tracer, string request, NamedPipeServer.Connection connection)
        {
            NamedPipeMessages.Message message = NamedPipeMessages.Message.FromString(request);
            if (string.IsNullOrWhiteSpace(message.Header))
            {
                return;
            }

            using (ITracer activity = this.tracer.StartActivity(message.Header, EventLevel.Informational, new EventMetadata { { nameof(request), request } }))
            {
                try
                {
                    this.HandleMessage(activity, message, connection);
                }
                catch (SerializationException ex)
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", this.etwArea);
                    metadata.Add("Header", message.Header);
                    metadata.Add("Exception", ex.ToString());

                    activity.RelatedError(metadata, $"Could not deserialize {this.requestDescription} request: {ex.Message}");
                }
            }
        }

        protected virtual void HandleMessage(
            ITracer tracer,
            NamedPipeMessages.Message message,
            NamedPipeServer.Connection connection)
        {
            switch (message.Header)
            {
                default:
                    this.requestDescription = UnknownRequestDescription;
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", this.etwArea);
                    metadata.Add("Header", message.Header);
                    tracer.RelatedWarning(metadata, "HandleNewConnection: Unknown request", Keywords.Telemetry);

                    this.TrySendResponse(tracer, NamedPipeMessages.UnknownRequest, connection);
                    break;
            }
        }

        private void TrySendResponse(
            ITracer tracer,
            string message,
            NamedPipeServer.Connection connection)
        {
            if (!connection.TrySendResponse(message))
            {
                tracer.RelatedError($"{nameof(this.TrySendResponse)}: Could not send response to client. Reply Info: {message}");
            }
        }
    }
}
