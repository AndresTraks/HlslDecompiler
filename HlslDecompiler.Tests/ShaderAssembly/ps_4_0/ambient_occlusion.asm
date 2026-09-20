ps_4_0
dcl_constantbuffer CB0[17], dynamicIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v1.xy
dcl_input_ps linear v2.xyz
dcl_output o0
dcl_temps 5
sample r0, v1.xyxx, t0, s0
mul r0.yzw, r0.xxx, v2.xyz
sample r1, v1.xyxx, t1, s0
mad r1.xyz, r1.xyz, l(2, 2, 2, 0), l(-1, -1, -1, 0)
mov r2.w, l(1)
mov r1.w, l(0)
mov r3.x, l(0)
loop
ige r3.y, r3.x, l(12)
breakc_nz r3.y
dp3 r3.y, cb0[r3.x + 4].xyz, r1.xyz
lt r3.z, l(0), r3.y
lt r3.y, r3.y, l(0)
iadd r3.y, -r3.z, r3.y
itof r3.y, r3.y
mul r3.yzw, r3.yyy, cb0[r3.x + 4].xyz
mad r2.xyz, r3.yzw, cb0[16].xxx, r0.yzw
dp4 r4.x, r2, cb0[0]
dp4 r4.y, r2, cb0[1]
dp4 r2.x, r2, cb0[3]
div r2.xy, r4.xy, r2.xx
mad r2.xy, r2.xy, l(0.5, -0.5, 0, 0), l(0.5, 0.5, 0, 0)
sample r4, r2.xyxx, t0, s0
mul r2.x, r4.x, cb0[16].y
mad r2.y, v2.z, r0.x, -r2.x
div_sat r2.y, cb0[16].x, |r2.y|
add r2.z, r2.z, cb0[16].z
ge r2.x, r2.x, r2.z
and r2.x, r2.y, r2.x
add r1.w, r1.w, r2.x
iadd r3.x, r3.x, l(1)
endloop
mad o0, -r1.w, l(0.0833333358, 0.0833333358, 0.0833333358, 0.0833333358), l(1, 1, 1, 1)
ret
