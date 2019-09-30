using System;
using System.IO;
using System.Web;
using System.Web.Caching;
using Sitecore.Analytics.Media;

namespace CacheableMediaRequest
{
    public class CacheableMediaRequestHandler : Sitecore.Resources.Media.MediaRequestHandler
    {
        private static readonly object cacheLock = new object();

        public override void ProcessRequest(HttpContext context)
        {
            base.ProcessRequest(context);

            if (context.Response.StatusCode != 200)
            {
                return;
            }

            string key = context.Request.Url.OriginalString;
            if (context.Cache[key] != null)
            {
                return;
            }

            MediaRequestTrackingInformation info = new MediaRequestTrackingInformation(this.GetMediaRequest(context.Request));
            if (info.IsTrackedRequest())
            {
                return;
            }

            ResponseFilterStream filter = new ResponseFilterStream(context.Response.Filter);
            filter.TransformStream += stream => CachingFilter(stream, context, key);

            context.Response.Filter = filter;
        }

        private static MemoryStream CachingFilter(MemoryStream ms, HttpContext context, string key)
        {
            byte[] buffer = ms.ToArray();

            if (buffer.LongLength > 2000000)
            {
                return ms;
            }

            lock (cacheLock)
            {
                context.Cache.Insert(key, new CacheableMedia
                {
                    Cache = context.Response.Cache,
                    Output = buffer,
                    Headers = context.Response.Headers,
                    StatusCode = context.Response.StatusCode,
                    ContentType = context.Response.ContentType,
                    ContentEncoding = context.Response.ContentEncoding,
                    HeaderEncoding = context.Response.HeaderEncoding,
                    Charset = context.Response.Charset,
                    ContentLength = buffer.Length,
                }, null, Cache.NoAbsoluteExpiration, TimeSpan.FromMinutes(10));
            }

            return ms;
        }
    }
}
