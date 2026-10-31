float4 main(float4 c : COLOR1, float4 p : TEXCOORD0) : COLOR { return c.zyxw + p.w; }
