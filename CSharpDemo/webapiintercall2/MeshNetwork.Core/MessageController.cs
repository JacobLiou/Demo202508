using System;
using System.Web.Http;

namespace MeshNetwork.Core
{
    public class MessageController : ApiController
    {
        // POST api/message 
        [HttpPost]
        public IHttpActionResult Post([FromBody]string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return BadRequest("Message cannot be empty");

            // Forward received message to the Node instance
            if (Node.Current != null)
            {
                Node.Current.ReceiveMessage(message);
            }
            else
            {
                Console.WriteLine("Warning: Node singleton is null.");
            }

            return Ok();
        }
    }
}
