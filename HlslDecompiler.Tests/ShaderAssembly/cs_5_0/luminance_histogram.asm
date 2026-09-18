cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer cb0[1], immediateIndexed
dcl_resource_texture2d (float,float,float,float) t0
dcl_uav_structured u0, 4
dcl_input vThreadID.xy
dcl_temps 1
dcl_thread_group 8, 8, 1
mov r0.xy, vThreadID.xy
mov r0.zw, l(0, 0, 0, 0)
ld r0.xyz, r0.xyz, t0.xyz
dp3 r0.x, r0.xyz, cb0[0].xyz
mul r0.x, r0.x, cb0[0].w
ftou r0.x, r0.x
umin r0.x, r0.x, l(15)
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
