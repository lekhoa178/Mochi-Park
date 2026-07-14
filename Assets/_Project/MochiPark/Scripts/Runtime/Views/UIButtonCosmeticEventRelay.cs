using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MochiPark.Runtime.Views
{
    public sealed class UIButtonCosmeticEventRelay : UIBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler,
        ISelectHandler,
        IDeselectHandler,
        ISubmitHandler
    {
        private UIButtonCosmeticService service;
        private Button button;

        internal void Bind(UIButtonCosmeticService boundService, Button boundButton)
        {
            service = boundService;
            button = boundButton;
        }

        internal void Clear(UIButtonCosmeticService boundService)
        {
            if (service != boundService)
            {
                return;
            }

            service = null;
            button = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            service?.NotifyPointerEnter(button);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            service?.NotifyPointerExit(button);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button == PointerEventData.InputButton.Left)
            {
                service?.NotifyPointerDown(button);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null || eventData.button == PointerEventData.InputButton.Left)
            {
                service?.NotifyPointerUp(button);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button == PointerEventData.InputButton.Left)
            {
                service?.NotifyPointerClick(button);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            service?.NotifyPointerEnter(button);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            service?.NotifyPointerExit(button);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            service?.NotifySubmit(button);
        }
    }
}
