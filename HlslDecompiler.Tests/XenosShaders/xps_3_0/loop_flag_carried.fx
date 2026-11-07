float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 v = texcoord;
	float flag = texcoord.y >= 0 ? 1 : 0;
	for (int i = 0; i < 255 && flag != 0; i++) {
		if (v.z > 0) {
			v = v + float4(0.25, 0.5, -1, -0.75);
		}
		flag = v.w > 0 ? 1 : 0;
	}
	for (int j = 0; j < 255 && flag == 0; j++) {
		if (v.x > 2) {
			v = v - float4(0.5, 0.25, 0.75, 1);
			flag = v.y > 0 ? 1 : 0;
		}
	}
	return v + flag;
}
