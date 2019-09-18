using System;
using System.IO;
using System.Text;
using System.Web;

namespace CacheableMediaRequest
{

    /// <summary>
    /// I stole this from https://weblog.west-wind.com/posts/2009/nov/13/capturing-and-transforming-aspnet-output-with-responsefilter
    /// </summary>
    public class ResponseFilterStream : Stream
    {
        /// <summary>
        ///     Stream that original content is read into
        ///     and then passed to TransformStream function
        /// </summary>
        private MemoryStream cacheStream = new MemoryStream(5000);

        /// <summary>
        ///     The original stream
        /// </summary>
        private readonly Stream stream;
        
        public ResponseFilterStream(Stream responseStream)
        {
            this.stream = responseStream;
        }
        
        /// <summary>
        ///     Determines whether the stream is captured
        /// </summary>
        private bool IsCaptured => this.CaptureStream != null || this.CaptureString != null || this.TransformStream != null || this.TransformString != null;

        /// <summary>
        ///     Determines whether the Write method is outputting data immediately
        ///     or delaying output until Flush() is fired.
        /// </summary>
        private bool IsOutputDelayed => this.TransformStream != null || this.TransformString != null;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }
        
        /// <summary>
        ///     Event that captures Response output and makes it available
        ///     as a MemoryStream instance. Output is captured but won't
        ///     affect Response output.
        /// </summary>
        public event Action<MemoryStream> CaptureStream;

        /// <summary>
        ///     Event that captures Response output and makes it available
        ///     as a string. Output is captured but won't affect Response output.
        /// </summary>
        public event Action<string> CaptureString;

        /// <summary>
        ///     Event that allows you transform the stream as each chunk of
        ///     the output is written in the Write() operation of the stream.
        ///     This means that that it's possible/likely that the input
        ///     buffer will not contain the full response output but only
        ///     one of potentially many chunks.
        ///     This event is called as part of the filter stream's Write()
        ///     operation.
        /// </summary>
        public event Func<byte[], byte[]> TransformWrite;
        
        /// <summary>
        ///     Event that allows you to transform the response stream as
        ///     each chunk of bytep[] output is written during the stream's write
        ///     operation. This means it's possibly/likely that the string
        ///     passed to the handler only contains a portion of the full
        ///     output. Typical buffer chunks are around 16k a piece.
        ///     This event is called as part of the stream's Write operation.
        /// </summary>
        public event Func<string, string> TransformWriteString;

        /// <summary>
        ///     This event allows capturing and transformation of the entire
        ///     output stream by caching all write operations and delaying final
        ///     response output until Flush() is called on the stream.
        /// </summary>
        public event Func<MemoryStream, MemoryStream> TransformStream;

        /// <summary>
        ///     Event that can be hooked up to handle Response.Filter
        ///     Transformation. Passed a string that you can modify and
        ///     return back as a return value. The modified content
        ///     will become the final output.
        /// </summary>
        public event Func<string, string> TransformString;
        
        protected virtual void OnCaptureStream(MemoryStream ms)
        {
            this.CaptureStream?.Invoke(ms);
        }
        
        private void OnCaptureStringInternal(MemoryStream ms)
        {
            if (this.CaptureString != null)
            {
                this.OnCaptureString(HttpContext.Current.Response.ContentEncoding.GetString(ms.ToArray()));
            }
        }

        protected virtual void OnCaptureString(string output)
        {
            this.CaptureString?.Invoke(output);
        }

        protected virtual byte[] OnTransformWrite(byte[] buffer)
        {
            return this.TransformWrite != null ? this.TransformWrite(buffer) : buffer;
        }

        private byte[] OnTransformWriteStringInternal(byte[] buffer)
        {
            Encoding encoding = HttpContext.Current.Response.ContentEncoding;
            return encoding.GetBytes(this.OnTransformWriteString(encoding.GetString(buffer)));
        }

        private string OnTransformWriteString(string value)
        {
            return this.TransformWriteString != null ? this.TransformWriteString(value) : value;
        }
        
        protected virtual MemoryStream OnTransformCompleteStream(MemoryStream ms)
        {
            return this.TransformStream != null ? this.TransformStream(ms) : ms;
        }
        
        /// <summary>
        ///     Allows transforming of strings
        ///     Note this handler is internal and not meant to be overridden
        ///     as the TransformString Event has to be hooked up in order
        ///     for this handler to even fire to avoid the overhead of string
        ///     conversion on every pass through.
        /// </summary>
        /// <param name="responseText"></param>
        /// <returns></returns>
        private string OnTransformCompleteString(string responseText)
        {
            this.TransformString?.Invoke(responseText);

            return responseText;
        }

        /// <summary>
        ///     Wrapper method form OnTransformString that handles
        ///     stream to string and vice versa conversions
        /// </summary>
        /// <param name="ms"></param>
        /// <returns></returns>
        internal MemoryStream OnTransformCompleteStringInternal(MemoryStream ms)
        {
            if (this.TransformString == null)
            {
                return ms;
            }

            string content = this.TransformString(HttpContext.Current.Response.ContentEncoding.GetString(ms.ToArray()));
            byte[] buffer = HttpContext.Current.Response.ContentEncoding.GetBytes(content);
            ms = new MemoryStream();
            ms.Write(buffer, 0, buffer.Length);

            return ms;
        }

        public override long Seek(long offset, SeekOrigin direction)
        {
            return this.stream.Seek(offset, direction);
        }

        public override void SetLength(long length)
        {
            this.stream.SetLength(length);
        }

        public override void Close()
        {
            this.stream.Close();
        }

        /// <summary>
        ///     Override flush by writing out the cached stream data
        /// </summary>
        public override void Flush()
        {
            if (this.IsCaptured && this.cacheStream.Length > 0)
            {
                // Check for transform implementations
                this.cacheStream = this.OnTransformCompleteStream(this.cacheStream);
                this.cacheStream = this.OnTransformCompleteStringInternal(this.cacheStream);

                this.OnCaptureStream(this.cacheStream);
                this.OnCaptureStringInternal(this.cacheStream);

                // write the stream back out if output was delayed
                if (this.IsOutputDelayed)
                {
                    this.stream.Write(this.cacheStream.ToArray(), 0, (int) this.cacheStream.Length);
                }

                // Clear the cache once we've written it out
                this.cacheStream.SetLength(0);
            }

            // default flush behavior
            this.stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.stream.Read(buffer, offset, count);
        }

        /// <summary>
        ///     Overriden to capture output written by ASP.NET and captured
        ///     into a cached stream that is written out later when Flush()
        ///     is called.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this.IsCaptured)
            {
                // copy to holding buffer only - we'll write out later
                this.cacheStream.Write(buffer, 0, count);
            }

            // just transform this buffer
            if (this.TransformWrite != null)
            {
                buffer = this.OnTransformWrite(buffer);
            }

            if (this.TransformWriteString != null)
            {
                buffer = this.OnTransformWriteStringInternal(buffer);
            }

            if (!this.IsOutputDelayed)
            {
                this.stream.Write(buffer, offset, buffer.Length);
            }
        }
    }
}