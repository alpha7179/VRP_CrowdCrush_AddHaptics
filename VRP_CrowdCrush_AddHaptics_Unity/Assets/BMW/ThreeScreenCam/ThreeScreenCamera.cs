using System.Collections.Generic;
using UnityEngine;

namespace ThreeScreenCam
{
    /// <summary>
    /// 3개, 4개 카메라를 한면에 그리게 도와주는 컴포넌트입니다.
    /// 화면 비율이 안맞을 경우 자동으로 모든 카메라가 16:9 비율로 렌더링됩니다.  
    /// </summary>
    public class ThreeScreenCamera : MonoBehaviour
    {
        /// <summary>
        /// 3개, 4개의 카메라를 넣어주세요.
        /// </summary>
        public List<Camera> cameras;
    
        /// <summary>
        /// 메인 카메라를 넣어주세요.
        /// </summary>
        public Camera       mainCam;

        /// <summary>
        /// 4개의 카메라를 사용할지 3개의 카메라를 사용할지 여부입니다.
        /// </summary>
        public bool isFourScreen = false;

        /// <summary>
        /// Start에서 한번만 실행할지 Update에서 계속 실행할지
        /// 화면 크기가 동적으로 변하는 환경에서 켜주세요.
        /// </summary>
        public bool isUpdateStartUpOnce = false;
    
        private bool  _isFirstUpdate = true;
        private Rect  _originMainCamRect;
        private float _originFieldOfView;

        private void OnEnable()
        {
            foreach (var cam in cameras)
            {
                cam.enabled = true;
            }

            _originMainCamRect = mainCam.rect;
            _originFieldOfView = mainCam.fieldOfView;
        }

        private void OnDisable()
        {
            foreach (var cam in cameras)
            {
                cam.enabled = false;
            }

            mainCam.enabled     = true;
            mainCam.rect        = _originMainCamRect;
            mainCam.fieldOfView = _originFieldOfView;
        }

        private void Start()
        {
            if (cameras.Count != 3 && cameras.Count != 4)
                Debug.LogWarning("cameras.Count != 3");
        }

        private void Update()
        {
            if (isUpdateStartUpOnce)
            {
                if (!_isFirstUpdate) return;

                UpdateScreenResolution();
                _isFirstUpdate = false;
            }
            else
            {
                UpdateScreenResolution();
            }
        }

        public void UpdateScreenResolution()
        {
            if (!isFourScreen)
                UpdateThreeScreenResolution();
            else
                UpdateThreeFourResolution();
        }

        /// <summary>
        /// 3개의 카메라를 16:9 비율로 합쳐서(16:3)에 렌더링한다.
        /// </summary>
        public void UpdateThreeScreenResolution()
        {
            // 카메라 3개 rect 기본값
            var viewRect0 = new Rect(0, 0, 0.33333333f, 1);
            var viewRect1 = new Rect(0.33333333f, 0, 0.33333333f, 1);
            var viewRect2 = new Rect(0.66666666f, 0, 0.33333333f, 1);

            var screenAspect = (float)Screen.width / Screen.height;
            var targetAspect = 16f / 3f;

            // width가 더 크면
            if (screenAspect > targetAspect)
            {
                // 비율은 그대로두고 뷰화면 양옆에 패딩 추가하기 위한 계산
                var normalizedWidth = targetAspect / screenAspect;
                var startRectX      = (1f - normalizedWidth) / 2f;
                var threeDivisions  = normalizedWidth / 3f;
                viewRect0.x = startRectX;
                viewRect1.x = startRectX + threeDivisions;
                viewRect2.x = startRectX + threeDivisions * 2f;

                viewRect0.width = threeDivisions;
                viewRect1.width = threeDivisions;
                viewRect2.width = threeDivisions;
            }
            else // height가 더 크면
            {
                // 비율은 그대로두고 뷰화면 위아래에 패딩 추가하기 위한 계산
                var normalizedHeight = screenAspect / targetAspect;

                viewRect0.y = viewRect1.y = viewRect2.y = (1f - normalizedHeight) / 2f;

                viewRect0.height = viewRect1.height = viewRect2.height = normalizedHeight;
            }

            // 카메라에 적용
            cameras[0].rect = viewRect0;
            cameras[1].rect = viewRect1;
            cameras[2].rect = viewRect2;


            // 카메라의 FOV를 Horizontal 기준으로 90으로 맞춤
            foreach (var camera in cameras)
            {
                camera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(90f, camera.aspect);
            }
        }

        /// <summary>
        /// 3개의 카메라를 16:9 비율로 합쳐서(64:9)에 렌더링한다. 단 왼쪽 한면은 검은색처리
        /// </summary>
        public void UpdateThreeFourResolution()
        {
            // 카메라 3개 rect 기본값
            var noViewRect = new Rect(0, 0, 0.25f, 1);
            var viewRect0  = new Rect(0.25f, 0, 0.25f, 1);
            var viewRect1  = new Rect(0.5f, 0, 0.25f, 1);
            var viewRect2  = new Rect(0.75f, 0, 0.25f, 1);

            var screenAspect = (float)Screen.width / Screen.height;
            var targetAspect = 64f / 9f;

            // width가 더 크면
            if (screenAspect > targetAspect)
            {
                // 비율은 그대로두고 뷰화면 양옆에 패딩 추가하기 위한 계산
                var normalizedWidth = targetAspect / screenAspect;
                var startRectX      = (1f - normalizedWidth) / 2f;
                var fourDivision    = normalizedWidth / 4f;
                noViewRect.x = startRectX;
                viewRect0.x  = startRectX + fourDivision;
                viewRect1.x  = startRectX + fourDivision * 2f;
                viewRect2.x  = startRectX + fourDivision * 3f;

                noViewRect.width = fourDivision;
                viewRect0.width  = fourDivision;
                viewRect1.width  = fourDivision;
                viewRect2.width  = fourDivision;
            }
            else // height가 더 크면
            {
                // 비율은 그대로두고 뷰화면 위아래에 패딩 추가하기 위한 계산
                var normalizedHeight = screenAspect / targetAspect;

                noViewRect.y = viewRect0.y = viewRect1.y = viewRect2.y = (1f - normalizedHeight) / 2f;

                noViewRect.height = viewRect0.height = viewRect1.height = viewRect2.height = normalizedHeight;
            }

            // 카메라에 적용
            cameras[3].rect = noViewRect;
            cameras[0].rect = viewRect0;
            cameras[1].rect = viewRect1;
            cameras[2].rect = viewRect2;

            // 카메라의 FOV를 Horizontal 기준으로 90으로 맞춘다.
            foreach (var camera in cameras)
            {
                camera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(90f, camera.aspect);
            }
        }
    }
}