using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;

namespace VolumeRendering.Utils
{

    public class ObjectController : MonoBehaviour
    {
        public Transform m_RaymarchedObject;

        static readonly string kMouseX = "Mouse X";
        static readonly string kMouseY = "Mouse Y";
        //static readonly string kMouseScroll = "Mouse ScrollWheel";

        [SerializeField, Range(1f, 10f)] protected float zoomSpeed = 2.5f, zoomDelta = 1f;
        [SerializeField, Range(1f, 15f)] protected float zoomMin = 5f, zoomMax = 15f;

        [SerializeField, Range(1f, 10f)] protected float rotateSpeed = 2.5f, rotateDelta = 2f;

        public Camera cam;
        protected Vector3 targetCamPosition;
        protected Quaternion targetRotation;

        protected void Start()
        {
            targetCamPosition = cam.transform.position;
            targetRotation = m_RaymarchedObject.transform.rotation;
        }

        protected void Update()
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;
            var dt = Time.deltaTime;
            Zoom(dt);
            Rotate(dt);
        }

        protected void Zoom(float dt)
        {
            var amount = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(amount) > 0f)
            {
                targetCamPosition += cam.transform.forward * zoomSpeed * amount;
                targetCamPosition = targetCamPosition.normalized * Mathf.Clamp(targetCamPosition.magnitude, zoomMin, zoomMax);
            }
            cam.transform.position = Vector3.Lerp(cam.transform.position, targetCamPosition, dt * zoomDelta);
        }

        protected void Rotate(float dt)
        {
            if (Input.GetMouseButton(0))
            {
                var mouseX = Input.GetAxis(kMouseX) * rotateSpeed;
                var mouseY = Input.GetAxis(kMouseY) * rotateSpeed;

                var up = m_RaymarchedObject.transform.InverseTransformDirection(cam.transform.up);
                targetRotation *= Quaternion.AngleAxis(-mouseX, up);

                var right = m_RaymarchedObject.transform.InverseTransformDirection(cam.transform.right);
                targetRotation *= Quaternion.AngleAxis(mouseY, right);
            }

            m_RaymarchedObject.transform.rotation = Quaternion.Slerp(m_RaymarchedObject.transform.rotation, targetRotation, dt * rotateDelta);
        }

    }

}


