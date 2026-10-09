using UnityEngine;

/// <summary>Fits the cabinet playfield and score panel edge to edge without stretching physics
/// or the camera projection. Portrait displays keep the table upright and the score above it;
/// landscape displays retain the rotated cabinet layout.</summary>
[ExecuteAlways, RequireComponent(typeof(Camera))]
public sealed class CabinetViewport : MonoBehaviour
{
    [SerializeField] private Transform table;
    [Tooltip("Géométrie utilisée pour le plan de coupe quand le repère de cadrage est distinct de la table physique.")]
    [SerializeField] private Transform tableGeometry;
    [SerializeField] private Vector2 horizontalLimits = new Vector2(-4.5f,4.5f);
    [SerializeField] private Vector2 lengthLimits = new Vector2(-.7f,21.1f);
    [SerializeField] private RectTransform scorePanel;
    [SerializeField, Range(.1f,.3f)] private float scoreScreenFraction = .15f;
    private Camera view;
    private float previousAspect = -1;
    private Matrix4x4 previousTable;
    private Bounds tableClipBounds;
    private bool hasClipBounds;

    private void OnEnable(){view=GetComponent<Camera>();CacheClipBounds();Fit();}
    private void OnValidate(){view=GetComponent<Camera>();CacheClipBounds();Fit();}
    private void LateUpdate(){if(table!=null && view!=null && (view.aspect!=previousAspect || table.localToWorldMatrix!=previousTable))Fit();}

    public void Fit()
    {
        if(table==null || view==null)return;
        float metricScale=Mathf.Abs(table.lossyScale.x);
        float width=(horizontalLimits.y-horizontalLimits.x)*metricScale;
        float length=(lengthLimits.y-lengthLimits.x)*Mathf.Abs(table.lossyScale.z);
        if(width<=0 || length<=0)return;
        bool portrait=view.aspect<1f;
        float cosine=Mathf.Clamp(portrait?width/(view.aspect*length):view.aspect*width/length,.25f,1f);
        float sine=Mathf.Sqrt(1-cosine*cosine);
        Vector3 centre=new Vector3((horizontalLimits.x+horizontalLimits.y)*.5f,0,(lengthLimits.x+lengthLimits.y)*.5f);
        transform.position=table.TransformPoint(centre+new Vector3(0,cosine*35,-sine*35));
        transform.rotation=Quaternion.LookRotation(table.TransformDirection(new Vector3(0,-cosine,sine)),table.forward)*Quaternion.Euler(0,0,portrait?0:90);
        view.orthographic=true;
        view.orthographicSize=portrait?Mathf.Max(width/(2*view.aspect),length*cosine*.5f):Mathf.Max(width*.5f,length*cosine/(2*view.aspect));
        // The score panel is two units from the camera. Keep it visible while
        // tightening the depth range around the table to reduce surface flicker.
        float farDepth=35*metricScale;
        if(hasClipBounds)
            for(int i=0;i<8;i++)
                farDepth=Mathf.Max(farDepth,Vector3.Dot(table.TransformPoint(Corner(tableClipBounds,i))-transform.position,transform.forward));
        view.nearClipPlane=.5f*metricScale;view.farClipPlane=farDepth+2*metricScale;view.rect=new Rect(0,0,1,1);
        if(scorePanel!=null && scorePanel.rect.width>0 && scorePanel.rect.height>0)
        {
            // Match the score panel to the screen orientation, independently of physics.
            Vector2 scoreCentre=portrait?new Vector2(.5f,1-scoreScreenFraction*.5f):new Vector2(1-scoreScreenFraction*.5f,.5f);
            scorePanel.position=view.ViewportToWorldPoint(new Vector3(scoreCentre.x,scoreCentre.y,2f*metricScale));
            scorePanel.rotation=Quaternion.LookRotation(transform.forward,portrait?transform.up:transform.right);
            float panelWidth=2*view.orthographicSize*(portrait?view.aspect:1f);
            float panelHeight=2*view.orthographicSize*scoreScreenFraction*(portrait?1f:view.aspect);
            scorePanel.localScale=new Vector3(panelWidth/scorePanel.rect.width,panelHeight/scorePanel.rect.height,1);
        }
        previousAspect=view.aspect;previousTable=table.localToWorldMatrix;
    }

    private void CacheClipBounds()
    {
        hasClipBounds=false;
        if(table==null)return;
        var geometry=tableGeometry!=null?tableGeometry:table;
        foreach(var renderer in geometry.GetComponentsInChildren<Renderer>())
        {
            if(!renderer.enabled || renderer.GetComponentInParent<Canvas>()!=null)continue;
            for(int i=0;i<8;i++)
            {
                var point=table.InverseTransformPoint(Corner(renderer.bounds,i));
                if(!hasClipBounds){tableClipBounds=new Bounds(point,Vector3.zero);hasClipBounds=true;}
                else tableClipBounds.Encapsulate(point);
            }
        }
    }

    private static Vector3 Corner(Bounds bounds,int index)
    {
        return bounds.center+Vector3.Scale(bounds.extents,new Vector3((index&1)==0?-1:1,(index&2)==0?-1:1,(index&4)==0?-1:1));
    }
}
