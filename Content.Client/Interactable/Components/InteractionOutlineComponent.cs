using System;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Interactable.Components
{
    [RegisterComponent]
    public sealed partial class InteractionOutlineComponent : Component
    {
        private static readonly ProtoId<ShaderPrototype> ShaderInRange = "SelectionOutlineInrange";
        private static readonly ProtoId<ShaderPrototype> ShaderOutOfRange = "SelectionOutline";

        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IEntityManager _entMan = default!;

        private const float DefaultWidth = 1;

        private bool _inRange;
        private ShaderInstance? _shader;
        private int _lastRenderScale;

        public void OnMouseEnter(EntityUid uid, bool inInteractionRange, int renderScale)
        {
            _lastRenderScale = renderScale;
            _inRange = inInteractionRange;
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite) && sprite.PostShader == null)
            {
                // Skip outline for very large sprites to avoid rendering artifacts
                // The render target for post-shaders is only 1.25x sprite size, which isn't enough
                // buffer space for outline sampling on 1.5x+ scaled sprites
                var spriteScale = (sprite.Scale.X + sprite.Scale.Y) / 2.0f;
                if (spriteScale > 1.3f)
                    return;
                
                // TODO why is this creating a new instance of the outline shader every time the mouse enters???
                _shader = MakeNewShader(sprite, inInteractionRange, renderScale);
                sprite.PostShader = _shader;
            }
        }

        public void OnMouseLeave(EntityUid uid)
        {
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
            {
                if (sprite.PostShader == _shader)
                    sprite.PostShader = null;
                sprite.RenderOrder = 0;
            }

            _shader?.Dispose();
            _shader = null;
        }

        public void UpdateInRange(EntityUid uid, bool inInteractionRange, int renderScale)
        {
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite)
                && sprite.PostShader == _shader
                && (inInteractionRange != _inRange || _lastRenderScale != renderScale))
            {
                _inRange = inInteractionRange;
                _lastRenderScale = renderScale;

                _shader = MakeNewShader(sprite, _inRange, _lastRenderScale);
                sprite.PostShader = _shader;
            }
        }

        private ShaderInstance MakeNewShader(SpriteComponent sprite, bool inRange, int renderScale)
        {
            var shaderName = inRange ? ShaderInRange : ShaderOutOfRange;

            var instance = _prototypeManager.Index<ShaderPrototype>(shaderName).InstanceUnique();
            
            // The outline shader samples texture pixels at outline_width distance
            // For scaled sprites, we need to reduce this to prevent sampling outside the
            // render target (which is sized as sprite screen bounds * 1.25)
            var spriteScale = (sprite.Scale.X + sprite.Scale.Y) / 2.0f;
            
            // Clamp outline width to 0.5 pixels for heavily scaled sprites (>1.5x)
            // This prevents the outline from sampling outside the render target bounds
            float outlineWidth;
            if (spriteScale > 1.5f)
            {
                outlineWidth = 0.5f;
            }
            else if (spriteScale > 1.2f)
            {
                outlineWidth = 0.75f;
            }
            else
            {
                outlineWidth = DefaultWidth * renderScale;
            }
            
            instance.SetParameter("outline_width", outlineWidth);
            return instance;
        }
    }
}
