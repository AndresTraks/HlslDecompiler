ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[10], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v1.xy
dcl_output o0
dcl_temps 2
sample_indexable(texture2d)(float,float,float,float) r0.z, v1.x, t0.x, s0
mad r0.xy, v1.xy, l(2, 2, 0, 0), l(-1, -1, 0, 0)
mov r0.w, l(1)
dp4 r1.x, r0, cb0[0]
dp4 r1.y, r0, cb0[1]
dp4 r1.z, r0, cb0[2]
dp4 r1.w, r0, cb0[3]
div r0, r1, r1.w
dp4 r1.z, r0, cb0[6]
dp4 r1.x, r0, cb0[4]
dp4 r1.y, r0, cb0[5]
ge r0.xyz, l(0.5, 0.5, 0.5, 0), |r1.xyz|
add r1.xy, r1.xy, l(0.5, 0.5, 0, 0)
sample_indexable(texture2d)(float,float,float,float) r1, r1.xyxx, t1, s0
and r0.xyz, r0.xyz, l(0x3f800000, 0x3f800000, 0x3f800000, 0x00000000)
mul r0.x, r0.x, r1.w
mul o0.xyz, r1.xyz, cb0[8].xyz
mul r0.x, r0.y, r0.x
mul r0.x, r0.z, r0.x
mul o0.w, r0.x, cb0[9].x
ret
