ps_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (uint,uint,uint,uint) t1
dcl_input_sv linear noperspective v0.xy
dcl_input_ps linear v1.xy
dcl_input_ps linear v2.xyz
dcl_output o0
dcl_temps 3
ftoi r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
ld r0, r0, t1
and r0.z, r0.x, l(65535)
utof r1.x, r0.z
ushr r0.x, r0.x, l(16)
mad r0.yzw, -v2.xyz, r0.yyy, cb0[0].xyz
utof r1.y, r0.x
mad r1.xy, r1.xy, l(0.0000305180438, 0.0000305180438, 0, 0), l(-1, -1, 0, 0)
add r0.x, -|r1.x|, l(1)
add r2.z, -|r1.y|, r0.x
max r0.x, -r2.z, l(0)
ge r1.zw, r1.xy, l(0, 0, 0, 0)
movc r1.zw, r1.zw, -r0.xx, r0.xx
add r2.xy, r1.zw, r1.xy
dp3 r0.x, r2.xyz, r2.xyz
rsq r0.x, r0.x
mul r1.xyz, r0.xxx, r2.xyz
dp3 r0.x, r0.yzw, r0.yzw
rsq r1.w, r0.x
sqrt r0.x, r0.x
div r0.x, r0.x, cb0[0].w
add_sat r0.x, -r0.x, l(1)
mul r0.x, r0.x, r0.x
mul r0.yzw, r0.yzw, r1.www
dp3_sat r0.y, r1.xyz, r0.yzw
mul r0.x, r0.y, r0.x
sample r1, v1.xyxx, t0, s0
mul r0.yzw, r1.xyz, cb0[1].xyz
mul o0.xyz, r0.xxx, r0.yzw
mov o0.w, l(1)
ret
