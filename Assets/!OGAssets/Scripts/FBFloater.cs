using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FruitBlast
{
    public class FBFloater : MonoBehaviour
    {
        internal Transform thisTransform;

        public Vector3 startingPosition;

        public bool localPosition = true;

        public float floatRangeX = 2;

        public float floatRangeY = 2;

        public float floatSpeedX = 5;
        public float floatSpeedY = 8;

        public float floatLean = 1;

        internal float hitFreeze = 0;
        internal float floatTime = 0;

        // Start is called before the first frame update
        void Start()
        {
            thisTransform = this.transform;

            if (localPosition == true) startingPosition = thisTransform.position;
        }

        // Update is called once per frame
        void Update()
        {
            if (hitFreeze > 0) hitFreeze -= Time.deltaTime;
            else
            {
                floatTime += Time.deltaTime;

                thisTransform.position = startingPosition + new Vector3(Mathf.Sin(floatSpeedX * floatTime) * floatRangeX, Mathf.Sin(floatSpeedY * floatTime) * floatRangeY, 0);

                thisTransform.eulerAngles = Vector3.forward * floatLean * Mathf.Sin(floatSpeedX * floatTime);

            }
        }

    }

}
