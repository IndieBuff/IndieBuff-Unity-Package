using System.Threading;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace IndieBUff.Editor
{
    public interface IResponseHandler
    {
        Task HandleResponse(string userMessage, VisualElement responseContainer, CancellationToken token);
        void HandleFullResponse(string aiMessage);
        void HandleError(VisualElement responseContainer);
        void Cleanup();
    }
}