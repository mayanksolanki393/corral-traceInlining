using System.Net;
using System.Text.Json;

namespace MessagePassing {
    public class HttpMaster : Master {
        private HttpListener _httpListener;

        public HttpMaster(List<string> prefixes) {
            _httpListener = new HttpListener();
            foreach (var prefix in prefixes) _httpListener.Prefixes.Add(prefix);
            _httpListener.Start();
        }

        public override void Start() {
            while (!isDone) {
                HttpListenerContext context = _httpListener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                ResponseThread(request, response);
            }
            Stop();
        }

        public override void Stop() {
            _httpListener.Stop();
            Console.WriteLine("Status: {0}", finalResult);
        }

        void ResponseThread(HttpListenerRequest request, HttpListenerResponse response) {
            if (request == null || response == null) return;

            Object deserialized = JsonSerializer.Deserialize(request.InputStream, typeof(Message));
            if (deserialized != null) {
                //Deserilize Request
                Message message = (Message) deserialized;

                //Handle Request
                List<Message> responseObj = HandleMessage(message);

                //Serialize Response and send back
                String responseString = JsonSerializer.Serialize(responseObj);
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                System.IO.Stream output = response.OutputStream;
                output.Write(buffer,0,buffer.Length);
                output.Close();
            }
        }
    }
}
