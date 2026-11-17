
using UnityEngine;

public class GridPoint : MonoBehaviour
{
    [SerializeField] private Sprite _red;
    private Sprite _green;
    [SerializeField] private SpriteRenderer _sprite;
    
    [SerializeField] private bool _buildable = true;
    private bool _placementPreview;
    private Color _normalColor;
    private bool _colorCaptured;
    private static Sprite _previewSprite;
    public bool Buildable
    {
        get { return _buildable; }
        set
        {
            _buildable = value;
            changeSprite(value);
        }
    }

    void Start()
    {
        _red = (Sprite)Resources.Load("red", typeof(Sprite));
        _green = (Sprite)Resources.Load("green", typeof(Sprite));
        changeSprite(_buildable);
        if (_sprite != null)
        {
            // Authored grid planes can sit above the actual floor and slice through
            // short traps. Move only their visual to the matching floor surface.
            float surface = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * 0.25f,
                Vector3.down, 1f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
                if (hit.normal.y > 0.5f && hit.collider.CompareTag(tag))
                    surface = Mathf.Max(surface, hit.point.y);
            if (!float.IsNegativeInfinity(surface))
            {
                Vector3 position = _sprite.transform.position;
                position.y = surface + 0.005f;
                _sprite.transform.position = position;
            }
        }
    }

    public void setSpriteRenderer(GameObject gridRender)
    {
        _sprite = gridRender.GetComponent<SpriteRenderer>();
    }

    public void changeSprite(bool isGreen)
    {
        if (_sprite == null) return;
        if (_green == null) _green = Resources.Load<Sprite>("green");
        if (_red == null) _red = Resources.Load<Sprite>("red");
        if (!_colorCaptured) { _normalColor = _sprite.color; _colorCaptured = true; }
        if (_placementPreview && isGreen)
        {
            // Yellow tint cannot replace green texture RGB. Share a white square with
            // the original cell size and tint only the locally selected free cell.
            if (_previewSprite == null)
            {
                var texture = Texture2D.whiteTexture;
                _previewSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), texture.width / _green.bounds.size.x);
                _previewSprite.name = "Placement candidate";
            }
            _sprite.sprite = _previewSprite;
            _sprite.color = new Color(1f, 0.8f, 0.05f, 0.65f);
        }
        else { _sprite.sprite = isGreen ? _green : _red; _sprite.color = _normalColor; }
            
    }

    public SpriteRenderer getSpriteRenderer()
    {
        return _sprite;
    }

    public void SetPlacementPreview(bool selected)
    {
        _placementPreview = selected;
        changeSprite(_buildable);
    }
}
