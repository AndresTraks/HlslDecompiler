ps_4_0
dcl_constantbuffer cb0[6], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_resource_texture2d (float,float,float,float) t2
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
sample r0, v0.xyxx, t2.yzxw, s0
mad r0.xy, v0.xy, l(2, 2, 0, 0), l(-1, -1, 0, 0)
mov r0.w, l(1)
dp4 r1.x, r0, cb0[0]
dp4 r1.y, r0, cb0[1]
dp4 r1.z, r0, cb0[2]
dp4 r0.x, r0, cb0[3]
div r0.xyz, r1.xyz, r0.xxx
add r0.xyz, -r0.xyz, cb0[4].xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r1.x, r0.w
sqrt r0.w, r0.w
div r0.w, r0.w, cb0[4].w
add_sat r0.w, -r0.w, l(1)
mul r0.xyz, r0.xyz, r1.xxx
sample r1, v0.xyxx, t1, s0
mad r1.xyz, r1.xyz, l(2, 2, 2, 0), l(-1, -1, -1, 0)
dp3_sat r0.x, r1.xyz, r0.xyz
sample r1, v0.xyxx, t0, s0
mul r1, r1, cb0[5]
mul r1, r0.x, r1
mul o0, r0.w, r1
ret
