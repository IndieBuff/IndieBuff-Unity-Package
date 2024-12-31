using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class ScriptResponseHandler : BaseResponseHandler
    {
        public ScriptResponseHandler(ScriptParser parser) : base(parser)
        {

        }

        protected override async Task OnStreamComplete()
        {
            var scriptParser = parser as ScriptParser;
            if (scriptParser != null)
            {
                scriptParser.FinishParsing();
            }

            await Task.CompletedTask;
        }

    }

}