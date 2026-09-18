ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 3
sample r0, v0.xyxx, t0, s0
mul r0, r0.xxxy, cb0[0].x
ushr r1.x, r0.z, l(23)
ushr r1.y, r0.z, l(16)
and r1.xy, r1.xy, l(255, 32768, 0, 0)
iadd r1.x, -r1.x, l(113)
ilt r1.z, r1.x, l(24)
and r2, r0, l(8388607, 2147483647, 2139095040, 8388607)
iadd r0.xy, r2.xw, l(8388608, 8388608, 0, 0)
ushr r0.x, r0.x, r1.x
and r0.x, r1.z, r0.x
ushr r0.x, r0.x, l(13)
iadd r1.x, r2.y, l(-939524096)
ushr r1.x, r1.x, l(13)
ult r1.z, r2.y, l(947912704)
movc r0.x, r1.z, r0.x, r1.x
ult r1.x, l(1207951360), r2.y
movc r0.x, r1.x, l(0.000000), r0.x
ushr r1.x, r0.z, l(13)
ushr r1.z, r0.z, l(3)
or r1.x, r1.x, r1.z
or r0.z, r0.z, r1.x
and r0.z, r0.z, l(1023)
iadd r0.z, r0.z, l(31744)
movc r0.z, r2.x, r0.z, l(0.000000)
ieq r1.x, r2.z, l(2139095040)
movc r0.x, r1.x, r0.z, r0.x
and r0.x, r0.x, l(32767)
iadd r0.x, r1.y, r0.x
ushr r0.z, r0.w, l(13)
ushr r1.x, r0.w, l(3)
or r0.z, r0.z, r1.x
or r0.z, r0.w, r0.z
and r0.z, r0.z, l(1023)
iadd r0.z, r0.z, l(31744)
movc r0.z, r2.w, r0.z, l(0.000000)
ushr r1.x, r0.w, l(23)
ushr r1.y, r0.w, l(16)
and r1.zw, r0.ww, l(0, 0, 2147483647, 2139095040)
and r1.xy, r1.xy, l(255, 32768, 0, 0)
iadd r0.w, -r1.x, l(113)
ushr r0.y, r0.y, r0.w
ilt r0.w, r0.w, l(24)
and r0.y, r0.w, r0.y
ushr r0.y, r0.y, l(13)
iadd r0.w, r1.z, l(-939524096)
ushr r0.w, r0.w, l(13)
ult r1.x, r1.z, l(947912704)
movc r0.y, r1.x, r0.y, r0.w
ult r0.w, l(1207951360), r1.z
ieq r1.x, r1.w, l(2139095040)
movc r0.y, r0.w, l(0.000000), r0.y
movc r0.y, r1.x, r0.z, r0.y
and r0.y, r0.y, l(32767)
iadd r0.y, r1.y, r0.y
ishl r0.y, r0.y, l(16)
iadd r0.z, r0.y, r0.x
ishl r0.x, r0.x, l(16)
and r1.xyz, r0.zzz, l(65535, 31744, 1023, 0)
mul o0.z, r0.z, cb0[0].y
ushr r0.z, r1.z, l(8)
movc r2.xy, r0.zz, l(0.000000, 0.000000, 0, 0), l(0, 0.000000, 0, 0)
movc r0.z, r0.z, r0.z, r1.z
ushr r0.w, r0.z, l(4)
movc r0.z, r0.w, r0.w, r0.z
movc r0.w, r0.w, r2.y, r2.x
iadd r1.w, r0.w, l(2)
ushr r2.x, r0.z, l(2)
movc r0.z, r2.x, r2.x, r0.z
movc r0.w, r2.x, r1.w, r0.w
ushr r0.z, r0.z, l(1)
iadd r1.w, r0.w, l(1)
movc r0.z, r0.z, r1.w, r0.w
iadd r0.z, -r0.z, l(10)
movc r0.z, r1.z, r0.z, l(0.000000)
ishl r0.w, r1.z, r0.z
ishl r0.z, r0.z, l(23)
iadd r0.z, -r0.z, l(947912704)
ishl r0.w, r0.w, l(13)
and r0.xw, r0.xw, l(-2147483648, 0, 0, 8380416)
iadd r0.z, r0.z, r0.w
movc r0.z, r1.z, r0.z, l(0)
ushr r0.w, r1.x, l(10)
ishl r0.w, r0.w, l(23)
and r0.w, r0.w, l(260046848)
iadd r0.w, r0.w, l(939524096)
ishl r1.x, r1.x, l(13)
and r1.x, r1.x, l(8380416)
iadd r0.w, r0.w, r1.x
iadd r1.x, r1.x, l(2139095040)
ieq r1.z, r1.y, l(31744)
movc r0.w, r1.z, r1.x, r0.w
movc r0.z, r1.y, r0.w, r0.z
and r0.z, r0.z, l(2147475456)
iadd o0.x, r0.x, r0.z
ushr r0.x, r0.y, l(16)
and r0.yzw, r0.yxx, l(0, -2147483648, 31744, 1023)
ushr r1.x, r0.w, l(8)
movc r1.yz, r1.xx, l(0, 0.000000, 0.000000, 0), l(0, 0, 0.000000, 0)
movc r1.x, r1.x, r1.x, r0.w
ushr r1.w, r1.x, l(4)
movc r1.xy, r1.ww, r1.wz, r1.xy
iadd r1.z, r1.y, l(2)
ushr r1.w, r1.x, l(2)
movc r1.xy, r1.ww, r1.wz, r1.xy
ushr r1.x, r1.x, l(1)
iadd r1.z, r1.y, l(1)
movc r1.x, r1.x, r1.z, r1.y
iadd r1.x, -r1.x, l(10)
movc r1.x, r0.w, r1.x, l(0.000000)
ishl r1.y, r0.w, r1.x
ishl r1.x, r1.x, l(23)
iadd r1.x, -r1.x, l(947912704)
ishl r1.y, r1.y, l(13)
and r1.y, r1.y, l(8380416)
iadd r1.x, r1.x, r1.y
movc r0.w, r0.w, r1.x, l(0)
ushr r1.x, r0.x, l(10)
ishl r0.x, r0.x, l(13)
and r0.x, r0.x, l(8380416)
ishl r1.x, r1.x, l(23)
and r1.x, r1.x, l(260046848)
iadd r1.x, r1.x, l(939524096)
iadd r1.x, r0.x, r1.x
iadd r0.x, r0.x, l(2139095040)
ieq r1.y, r0.z, l(31744)
movc r0.x, r1.y, r0.x, r1.x
movc r0.x, r0.z, r0.x, r0.w
and r0.x, r0.x, l(2147475456)
iadd o0.y, r0.y, r0.x
mov o0.w, l(1)
ret
