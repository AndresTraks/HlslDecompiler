float4 packing;

SamplerState samp;
Texture2D source;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float2 t0 = source.Sample(samp, texcoord).xy;
	int2 t1 = asint(t0 * packing.x);
	int4 t2 = int4(t1.y & int2(8388607, 2147483647), asint(t0.x * packing.x) & int2(8388607, 2147483647));
	int2 t3 = 113 - (((uint2)t1.yx >> 23) & 255);
	int t4 = ((((uint)t1.y >> 16) & 32768) + (((t1.y & 2139095040) == 2139095040 ? t2.x ? ((t1.y | (((uint)t1.y >> 13) | ((uint)t1.y >> 3))) & 1023) + 31744 : 31744 : (uint)t2.y > 1207951360 ? 31743 : (uint)t2.y < 947912704 ? (t3.x < 24 ? (uint)t2.x + 8388608 >> t3.x : 0) >> 13 : (uint)t2.y - 939524096 >> 13) & 32767)) * 65536;
	int t5 = (uint)t4 >> 16;
	int2 t6 = t5 & int2(31744, 1023);
	int t7 = (t5 * 8192) & 8380416;
	int t8 = (uint)t6.y >> 8;
	uint t9 = t8 ? t8 : t6.y;
	uint t10 = t9 >> 4;
	int2 t11 = int2(t10 ? t10 : t9, t10 ? t8 ? 12 : 4 : t8 ? 8 : 0);
	uint t12 = (uint)t11.x >> 2;
	int t13 = t12 ? t11.y + 2 : t11.y;
	int t14 = t6.y ? 10 - ((t12 ? t12 : t11.x) >> 1 ? t13 + 1 : t13) : 11;
	int t15 = (((uint)t1.x >> 16) & 32768) + (((t1.x & 2139095040) == 2139095040 ? t2.z ? ((t1.x | (((uint)t1.x >> 13) | ((uint)t1.x >> 3))) & 1023) + 31744 : 31744 : (uint)t2.w > 1207951360 ? 31743 : (uint)t2.w < 947912704 ? (t3.y < 24 ? (uint)t2.z + 8388608 >> t3.y : 0) >> 13 : (uint)t2.w - 939524096 >> 13) & 32767);
	int t16 = t4 + t15;
	int3 t17 = t16 & int3(65535, 31744, 1023);
	int t18 = (t17.x * 8192) & 8380416;
	int t19 = (uint)t17.z >> 8;
	uint t20 = t19 ? t19 : t17.z;
	uint t21 = t20 >> 4;
	uint t22 = t21 ? t21 : t20;
	uint t23 = t22 >> 2;
	int t24 = t21 ? t19 ? 12 : 4 : t19 ? 8 : 0;
	int t25 = t23 ? t24 + 2 : t24;
	int t26 = t17.z ? 10 - ((t23 ? t23 : t22) >> 1 ? t25 + 1 : t25) : 11;
	return float4(asfloat(((t15 * 65536) & -2147483648) + ((t17.y ? t17.y == 31744 ? t18 + 2139095040 : ((((uint)t17.x >> 10) * 8388608) & 260046848) + 939524096 + t18 : t17.z ? 947912704 - (t26 * 8388608) + (((t17.z << t26) * 8192) & 8380416) : 0) & 2147475456)), asfloat((t4 & -2147483648) + ((t6.x ? t6.x == 31744 ? t7 + 2139095040 : ((((uint)t5 >> 10) * 8388608) & 260046848) + 939524096 + t7 : t6.y ? 947912704 - (t14 * 8388608) + (((t6.y << t14) * 8192) & 8380416) : 0) & 2147475456)), asfloat(t16) * packing.y, 1);
}
