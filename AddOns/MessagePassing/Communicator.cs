using System.Text;
using System.Text.Json;

namespace MessagePassing {

    public abstract class Communicator {
        protected int Id;
        
        public int GetId(){
            return Id;
        }

        public abstract int Register();
        public abstract bool Unregister();
        public abstract bool Ping();
        public abstract bool ReportFinished(object body);
        public abstract bool ReportCrash(object body);
        public abstract bool Broadcast(object message);
        public abstract bool Unicast(int receiverId, object body);
        public abstract List<object> CheckMessages();
    }

    public class HttpCommunicator : Communicator {
        private HttpClient httpClient;
        private string serverUrl;

        public HttpCommunicator(string serverUrl, int senderId=-1) {
            this.httpClient = new HttpClient();
            httpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            this.serverUrl = serverUrl;
            this.Id = senderId;
        }

        private List<Message> SendMessage(Message request, Object body=null) {
            if (Id == -1 && request.requestType != "register") {
                throw new Exception("You must call register on Communicator before calling any other method");
            }

            request.senderId = Id;
            request.body = body == null ? "" : body;

            var json = JsonSerializer.Serialize(request);
            var data = new StringContent(json, Encoding.UTF8, "application/json");
            var response = httpClient.PostAsync(serverUrl, data).Result;
            List<Message> reply = (List<Message>) JsonSerializer.Deserialize(response.Content.ReadAsStream(), typeof(List<Message>));
            return reply;

        }

        public override int Register() {
            List<Message> response = SendMessage(Message.REGISTER);
            if (response.Count != 1) {
                throw new Exception("Unexpected Response on Register");
            }

            Id = ((JsonElement)response[0].body).GetInt32();
            return Id;
        }

        public override bool Unregister() {
            List<Message> response = SendMessage(Message.UNREGISTER);
            if (response.Count != 1) {
                throw new Exception("Unexpected Response on Unregister");
            }

            return ((JsonElement)response[0].body).GetBoolean();
        }

        public override bool Ping() {
            List<Message> response = SendMessage(Message.PING);
            if (response.Count != 1) {
                throw new Exception("Unexpected Response on Ping");
            }
            return ((JsonElement)response[0].body).GetBoolean();
        }

        public override bool ReportCrash(object body) {
            List<Message> response = SendMessage(Message.CRASH, body);
            if (response.Count != 1) {
                throw new Exception("Unexpected Response on ReportCrash");
            }

            return ((JsonElement)response[0].body).GetBoolean();
        }

        public override bool ReportFinished(object body) {
            List<Message> response = SendMessage(Message.FINISHED, body);
            if (response.Count != 1) {
                throw new Exception("Unexpected Response on ReportFinished");
            }

            return ((JsonElement)response[0].body).GetBoolean();
        }

        public override bool Broadcast(object body) {
            List<Message> response = SendMessage(Message.BROADCAST, body);
            if (response.Count != 1) {
                throw new Exception("Unexpected Response on BroadCast");
            }

            return ((JsonElement)response[0].body).GetBoolean();
        }

        public override bool Unicast(int receiverId, object body) {
            Message message = Message.UNICAST;
            message.receiverId = receiverId;
            List<Message> response = SendMessage(message, body);
            if (response.Count != 1) {
                throw new Exception("Unexpected Response on Unicast");
            }

            return ((JsonElement)response[0].body).GetBoolean();
        }

        public override List<object> CheckMessages() {
            List<Message> response = SendMessage(Message.CHECK_MSG);
            List<object> reply = new List<object>();
            
            // no new messages to report
            if (response.Count == 1 && response[0].requestType == "pong") {
                return reply;
            }
            else {
                foreach (var message in response) {
                    reply.Add(message.body);
                }
                return reply;
            }
        }
    }
}