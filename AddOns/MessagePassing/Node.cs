using System.Text.Json;

namespace MessagePassing {

    [Serializable]
    public class Message {
        public enum Type {REQUEST, LOGISTIC, NOREPLY};
        public Type messageType {get; set;}
        public String requestType {get; set;}
        public int senderId {get; set;}
        public int receiverId {get; set;}
        public Object body {get; set;}

        public static Message REGISTER = new Message(Message.Type.LOGISTIC, "register");
        public static Message UNREGISTER = new Message(Message.Type.LOGISTIC, "unregister");
        public static Message PING = new Message(Message.Type.LOGISTIC, "ping");
        public static Message CRASH = new Message(Message.Type.LOGISTIC, "crash");
        public static Message FINISHED = new Message(Message.Type.LOGISTIC, "finished");
        public static Message UNICAST = new Message(Message.Type.REQUEST, "unicast");
        public static Message BROADCAST = new Message(Message.Type.REQUEST, "broadcast");
        public static Message CHECK_MSG = new Message(Message.Type.REQUEST, "checkMessages");

        public Message(Type messageType, String requestType, Object body=null) {
            this.senderId = 0;
            this.messageType = messageType;
            this.requestType = requestType;
            this.body = body;
        }
    }

    public abstract class Node {
        public int id {get;}
        public bool isRunning; // will be used by server to wait for all the clients to finish
        public bool isDone;

        public Node(int id) {
            this.id = id;
            this.isRunning = true;
        }

        public virtual void finish() {
            isDone = true;
        } 
    }

    public abstract class Master : Node {
        protected List<Node> nodes;
        protected Dictionary<string, object> finalResult;
        private Dictionary<string, List<string>> summaries;

        public Master() : base(0) {
            nodes = new List<Node>();
            nodes.Add(this);
            summaries = new Dictionary<string, List<string>>();
        }
        
        public abstract void Start();
        public abstract void Stop();
        public virtual List<Message> HandleRequest(Message requestMessage) {
            if (!isRunning) {
                return new List<Message>{new Message(Message.Type.LOGISTIC, "pong", "finish")};
            }

            switch (requestMessage.requestType) {
                case "broadcast":
                    Console.WriteLine("Broadcast from Client: {0}", requestMessage.senderId);
                    for (int i=1; i<nodes.Count; i++) {
                        if (i != requestMessage.senderId) {
                            ((Slave) nodes[i]).SendMessage(requestMessage);
                        }
                    }
                    return new List<Message>{new Message(Message.Type.LOGISTIC, "pong", true)};

                case "unicast":
                    Slave recieverNode = (Slave) nodes[requestMessage.receiverId];
                    recieverNode.SendMessage(requestMessage);
                    return new List<Message>{new Message(Message.Type.LOGISTIC, "pong", true)};

                case "checkMessages":
                    Console.WriteLine("CheckMessages from Client: {0}", requestMessage.senderId);
                    Slave slave = (Slave) nodes[requestMessage.senderId];
                    if (slave.HasMessages()) {
                        return slave.PopMessages();
                    }
                    else {
                        return new List<Message>{new Message(Message.Type.LOGISTIC, "pong", true)};
                    }

                default:
                    throw new NotImplementedException("Not sure how to handle : " + requestMessage.requestType);
            }
        }

        public virtual Message HandleLogistic(String requestType, int senderId, Object body) {
            switch (requestType) {
                case "register": {
                    lock (nodes) {
                        Slave newSlave = new Slave(nodes.Count);
                        this.nodes.Add(newSlave);
                        return new Message(Message.Type.LOGISTIC, "id", newSlave.id);
                    }
                }
                case "unregister": {
                    Console.WriteLine("Unregister Recieved: {0}", senderId);
                    Slave slave = (Slave) nodes[senderId];
                    slave.isRunning = false;
                    isDone = nodes.Where(x => x.isRunning).Count() <= 1;
                    return new Message(Message.Type.LOGISTIC, "finished", true);
                }
                case "ping": {
                    Slave slave = (Slave) nodes[senderId];
                    slave.CheckIn();
                    if (slave.isDone) {
                        return new Message(Message.Type.LOGISTIC, "pong", false);
                    }
                    return new Message(Message.Type.LOGISTIC, "pong", true);
                }
                case "crash": {
                    Slave slave = (Slave) nodes[senderId];
                    slave.isRunning = false;
                    Console.WriteLine("Crash Reported By {0}:\n{1}", senderId, body);
                    slave.crashReport = body;
                    isDone = nodes.Where(x => x.isRunning).Count() <= 1;
                    return new Message(Message.Type.LOGISTIC, "pong", true);
                }
                case "finished": {
                    Console.WriteLine("Finish Recieved: {0}", senderId);
                    //Save Only FirstResult
                    if (finalResult == null)
                        finalResult = ((JsonElement)body).Deserialize<Dictionary<String, Object>>();

                    for (int i=0; i<nodes.Count; i++) {
                        nodes[i].finish();
                    }
                    this.isDone = nodes.Where(x => x.isRunning).Count() <= 1;

                    return new Message(Message.Type.LOGISTIC, "finished", true);
                }
                default:
                    throw new NotImplementedException("Not sure how to handle : " + requestType);
            }
        }

        public virtual List<Message> HandleMessage(Message message) {
            List<Message> reply = new List<Message>();
            switch (message.messageType) {
                case Message.Type.REQUEST:
                    reply = HandleRequest(message);
                    break;
                case Message.Type.LOGISTIC:
                    Message replyMessage = HandleLogistic(message.requestType, message.senderId, message.body);
                    if (replyMessage.messageType != Message.Type.NOREPLY) {
                        reply.Add(replyMessage);
                    }
                    break;
                default:
                    throw new NotImplementedException("Not sure how to handle : " + message.messageType);
            }

            foreach(var msg in reply) {
                msg.senderId = this.id;
            }

            return reply;
        }
        
        //This is a vitual representation of the Slave for Master
        public class Slave : Node {
            private List<Message> messages;
            private DateTime lastCheckin;
            public object crashReport;
            
            public Slave(int id) : base(id){
                this.messages = new List<Message>();
                this.isDone = false;
                this.crashReport = "";
            }

            public virtual object SendMessage(Message message) {
                lock(this) {
                    messages.Add(message);
                }
                return null;
            }

            public List<Message> PopMessages() {
                lock(this) {
                    List<Message> messagesCopy = messages;
                    messages = new List<Message>();
                    return messagesCopy;
                }
            }

            public void CheckIn() {
                lastCheckin = DateTime.Now;
            }

            public bool HasMessages(){
                return messages.Count > 0;
            }
        }
    }
}
