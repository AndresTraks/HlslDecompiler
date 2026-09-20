float4 packing;

SamplerState samp;
Texture2D source;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	int2 t0 = asint(source.Sample(samp, texcoord).xy * packing.x);
	int4 t1 = int4(t0.y & int2(8388607, 2147483647), asint(source.Sample(samp, texcoord).x * packing.x) & int2(8388607, 2147483647));
	int2 t2 = 113 - (((uint2)t0.yx >> 23) & 255);
	int t3 = ((((uint)t0.y >> 16) & 32768) + (((t0.y & 2139095040) == 2139095040 ? t1.x ? ((t0.y | (((uint)t0.y >> 13) | ((uint)t0.y >> 3))) & 1023) + 31744 : 31744 : (uint)t1.y > 1207951360 ? 31743 : (uint)t1.y < 947912704 ? (t2.x < 24 ? (uint)t1.x + 8388608 >> t2.x : 0) >> 13 : (uint)t1.y - 939524096 >> 13) & 32767)) * 65536;
	int t4 = (uint)t3 >> 16;
	int2 t5 = t4 & int2(31744, 1023);
	int t6 = (t4 * 8192) & 8380416;
	int t7 = (uint)t5.y >> 8;
	uint t8 = t7 ? t7 : t5.y;
	uint t9 = t8 >> 4;
	int2 t10 = int2(t9 ? t9 : t8, t9 ? t7 ? 12 : 4 : t7 ? 8 : 0);
	uint t11 = (uint)t10.x >> 2;
	int t12 = t11 ? t10.y + 2 : t10.y;
	int t13 = t5.y ? 10 - ((t11 ? t11 : t10.x) >> 1 ? t12 + 1 : t12) : 11;
	int t14 = (((uint)t0.x >> 16) & 32768) + (((t0.x & 2139095040) == 2139095040 ? t1.z ? ((t0.x | (((uint)t0.x >> 13) | ((uint)t0.x >> 3))) & 1023) + 31744 : 31744 : (uint)t1.w > 1207951360 ? 31743 : (uint)t1.w < 947912704 ? (t2.y < 24 ? (uint)t1.z + 8388608 >> t2.y : 0) >> 13 : (uint)t1.w - 939524096 >> 13) & 32767);
	int t15 = t3 + t14;
	int3 t16 = t15 & int3(31744, 65535, 1023);
	int t17 = (t16.y * 8192) & 8380416;
	int t18 = (uint)t16.z >> 8;
	uint t19 = t18 ? t18 : t16.z;
	uint t20 = t19 >> 4;
	uint t21 = t20 ? t20 : t19;
	uint t22 = t21 >> 2;
	int t23 = t20 ? t18 ? 12 : 4 : t18 ? 8 : 0;
	int t24 = t22 ? t23 + 2 : t23;
	int t25 = t16.z ? 10 - ((t22 ? t22 : t21) >> 1 ? t24 + 1 : t24) : 11;
	return float4(asfloat(((t14 * 65536) & -2147483648) + ((t16.x ? t16.x == 31744 ? t17 + 2139095040 : ((((uint)t16.y >> 10) * 8388608) & 260046848) + 939524096 + t17 : t16.z ? 947912704 - (t25 * 8388608) + (((t16.z << t25) * 8192) & 8380416) : 0) & 2147475456)), asfloat((t3 & -2147483648) + ((t5.x ? t5.x == 31744 ? t6 + 2139095040 : ((((uint)t4 >> 10) * 8388608) & 260046848) + 939524096 + t6 : t5.y ? 947912704 - (t13 * 8388608) + (((t5.y << t13) * 8192) & 8380416) : 0) & 2147475456)), asfloat(t15) * packing.y, 1);
}
