using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseMovement : MonoBehaviour
{
    
    public float mouseSensitivity = 200f;
 
    public Transform playerBody;

    float xRotation = 0f;
 
    void Start()
    {
      //Locking the cursor to the middle of the screen and making it invisible
      Cursor.lockState = CursorLockMode.Locked;
    }
 
    void Update()
    {
      if(!InventorySystem.Instance.isOpen)
      {
       float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
       float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
 
       //control rotation around x axis (Look up and down)
       xRotation -= mouseY;
       xRotation = Mathf.Clamp(xRotation, -90f, 90f);

       transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
       playerBody.Rotate(Vector3.up * mouseX); 
      }
    }
}
