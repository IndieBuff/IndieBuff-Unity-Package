using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class PrototypeResponseHandler : BaseResponseHandler
    {
        public PrototypeResponseHandler(PrototypeParser parser) : base(parser)
        {

        }

        protected override async Task OnStreamComplete()
        {
            var prototypeParser = parser as PrototypeParser;
            if (prototypeParser != null)
            {
                prototypeParser.FinishParsing();
            }

            await Task.CompletedTask;
        }


    }

}