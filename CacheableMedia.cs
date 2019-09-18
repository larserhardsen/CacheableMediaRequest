using System.Collections.Specialized;
using System.Web;

namespace CacheableMediaRequest
{
    public class CacheableMedia
    {
        public byte[] Output { get; set; }
        public int StatusCode { get; set; }
        public HttpCachePolicy Cache { get; set; }
        public NameValueCollection Headers { get; set; }
        public string ContentType { get; set; }
    }
}
