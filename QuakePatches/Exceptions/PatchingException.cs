using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace QuakePatches.Exceptions
{
    public class PatchingException : Exception
    {
        public PatchingException()
        {
        }

        public PatchingException(string message) : base(message)
        {
        }

        public PatchingException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected PatchingException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
