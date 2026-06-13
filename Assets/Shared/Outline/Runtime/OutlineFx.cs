using UnityEngine;

//  OutlineFx © NullTale - https://x.com/NullTale/
namespace OutlineFx
{
    [ExecuteAlways]
    public class OutlineFx : Outline
    {
        public  Color _color = Color.clear;

        public override Color Color
        {
            get => _color;
            set => _color = value;
        }
        
        public float Alpha
        {
            get => _color.a;
            set => _color.a = value;
        }
    }
}