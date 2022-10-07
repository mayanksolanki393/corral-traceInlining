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
            while (!isDone || nodes.Where(x => x.isRunning).Count() > 1) {
                HttpListenerContext context = _httpListener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                ResponseThread(request, response);
                //Thread _responseThread = new Thread(() => ResponseThread(request, response));
                //_responseThread.IsBackground = true;
                //_responseThread.Start();
            }
        }

        public override void Stop() {
            _httpListener.Stop();
        }

        void ResponseThread(HttpListenerRequest request, HttpListenerResponse response) {
            if (request == null || response == null) return;

            Object deserialized = JsonSerializer.Deserialize(request.InputStream, typeof(Message));
            if (deserialized != null) {
                //Deserilize Request
                Message message = (Message) deserialized;
                Console.WriteLine("senderId (before): {0}", message.senderId);

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

            if (isDone) Stop(); 
        }
    }
}
