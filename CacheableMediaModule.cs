using System;
using System.Web;

namespace CacheableMediaRequest
{
    public class CacheableMediaModule : IHttpModule
    {
        public void Dispose()
        {
        }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += OnBeginRequest;
        }

        private static void OnBeginRequest(object sender, EventArgs e)
        {
            HttpContext context = ((HttpApplication)sender).Context;
         
            string key = context.Request.Url.OriginalString;
            if (context.Cache[key] == null)
            {
                return;
            }

            if (context.Request.QueryString["killCache"] != null)
            {
                context.Cache.Remove(key);
                return;
            }
            
            CacheableMedia media = (CacheableMedia)context.Cache[key];
            WriteResponse(context, media);
        }

        private static void WriteResponse(HttpContext context, CacheableMedia media)
        {
            HttpResponse response = context.Response;

            foreach (string headerKey in media.Headers.AllKeys)
            {
                if (response.Headers[headerKey] != null)
                {
                    response.Headers[headerKey] = media.Headers[headerKey];
                }
            }

            if (context.IsDebuggingEnabled)
            {
                response.Headers.Add("X-EvilCache", "True");
            }

            response.OutputStream.Write(media.Output, 0, media.Output.Length);
            response.ContentType = media.ContentType;
            response.StatusCode = media.StatusCode;
            response.ContentEncoding = media.ContentEncoding;
            response.HeaderEncoding = media.HeaderEncoding;
            response.Charset = media.Charset;
            response.Cache.SetLastModified(media.Cache.GetUtcLastModified());
            response.Cache.SetCacheability(media.Cache.GetCacheability());
            response.Cache.SetMaxAge(media.Cache.GetMaxAge());

            if (media.Cache.GetExpires() != DateTime.MinValue)
            {
                response.Cache.SetExpires(media.Cache.GetExpires());
            }

            response.End();
        }
    }
}