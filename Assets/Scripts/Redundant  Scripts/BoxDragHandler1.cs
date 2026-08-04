using UnityEngine;

public class BoxDragHandler1 : MonoBehaviour
{
    private Vector3 initialPosition;
    private Camera mainCamera;
    private Node highlightedNode = null;

    private void Start()
    {
        initialPosition = transform.position;
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                Node node = hit.collider.GetComponent<Node>();
                if (node != null && !node.isOccupied)
                {
                    highlightedNode = node;
                    HighlightNode(node, true); 
                }
            }
        }

        if (Input.GetMouseButton(0))
        {
            transform.position = GetMouseWorldPosition();
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (highlightedNode != null && !highlightedNode.isOccupied)
            {
                // باکس را روی نود قرار دهید و نود را اشغال شده کنید
                transform.position = highlightedNode.transform.position;
                highlightedNode.isOccupied = true;
            }
            else
            {
                // برگرداندن باکس به موقعیت اولیه
                transform.position = initialPosition;
            }

            // حذف هایلایت
            if (highlightedNode != null)
            {
                HighlightNode(highlightedNode, false);
                highlightedNode = null;
            }
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        return mainCamera.ScreenToWorldPoint(mousePos);
    }

    private void HighlightNode(Node node, bool highlight)
    {
        Renderer renderer = node.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = highlight ? Color.yellow : Color.white;
        }
    }
}
