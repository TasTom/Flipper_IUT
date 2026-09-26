using UnityEngine;

/// <summary>Fits the cabinet playfield and score panel edge to edge without stretching physics
/// or the camera projection. The table's viewing angle adapts to the display aspect ratio.</summary>
[ExecuteAlways, RequireComponent(typeof(Camera))]
public sealed class CabinetViewport : MonoBehaviour
{
    [SerializeField] private Transform table;
    [SerializeField] private Vector2 horizontalLimits = new Vector2(-4.5f,4.5f);
    [SerializeField] private Vector2 lengthLimits = new Vector2(-.7f,21.1f);
    [SerializeField] private RectTransform scorePanel;
    [SerializeField, Range(.1f,.3f)] private float scoreScreenFraction = .15f;
    private Camera view;
    private float previousAspect = -1;
    private Matrix4x4 previousTable;

    private void OnEnable(){view=GetComponent<Camera>();Fit();}
    private void OnValidate(){view=GetComponent<Camera>();Fit();}
    private void LateUpdate(){if(table!=null && (view.aspect!=previousAspect || table.localToWorldMatrix!=previousTable))Fit();}

    public void Fit()
    {
        if(table==null || view==null)return;
        float width=horizontalLimits.y-horizontalLimits.x;
        float length=lengthLimits.y-lengthLimits.x;
        if(width<=0 || length<=0)return;
        float cosine=Mathf.Clamp(view.aspect*width/length,.25f,1f);
        float sine=Mathf.Sqrt(1-cosine*cosine);
        Vector3 centre=new Vector3((horizontalLimits.x+horizontalLimits.y)*.5f,0,(lengthLimits.x+lengthLimits.y)*.5f);
        transform.position=table.TransformPoint(centre+new Vector3(0,cosine*35,-sine*35));
        transform.rotation=Quaternion.LookRotation(table.TransformDirection(new Vector3(0,-cosine,sine)),table.forward)*Quaternion.Euler(0,0,90);
        view.orthographic=true;view.orthographicSize=Mathf.Max(width*.5f,length*cosine/(2*view.aspect));
        view.nearClipPlane=.1f;view.farClipPlane=100;view.rect=new Rect(0,0,1,1);
        if(scorePanel!=null && scorePanel.rect.width>0 && scorePanel.rect.height>0)
        {
            // The cabinet roll is also applied to the display. A camera-facing plane
            // keeps the score within the screen and clear of raised table furniture.
            scorePanel.position=view.ViewportToWorldPoint(new Vector3(1-scoreScreenFraction*.5f,.5f,2f));
            scorePanel.rotation=Quaternion.LookRotation(transform.forward,transform.right);
            scorePanel.localScale=new Vector3(2*view.orthographicSize/scorePanel.rect.width,
                2*view.orthographicSize*view.aspect*scoreScreenFraction/scorePanel.rect.height,1);
        }
        previousAspect=view.aspect;previousTable=table.localToWorldMatrix;
    }
}
