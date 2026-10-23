cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_uav_typed_texture2d (float,float,float,float) u0
dcl_input vThreadID.xy
dcl_temps 5
dcl_thread_group 8, 8, 1
utof r0.xy, vThreadID.xy
add r0.xy, r0.xy, l(0.25, 0.25, 0, 0)
mul r0.zw, r0.xy, cb0[0].zw
and r1.x, cb0[1].x, l(3)
switch r1.x
case l(0)
mad r2.xy, cb0[0].zw, l(0.25, 0.25, 0, 0), r0.zw
sample_l_indexable(texture2d)(float,float,float,float) r1, r2.xyxx, t0, s0, l(0)
break
case l(1)
sample_l_indexable(texture2d)(float,float,float,float) r2, r0.zwzz, t0, s0, l(0)
mul r3.x, cb0[0].z, l(0.5)
mov r3.y, l(0)
mad r3.xy, cb0[0].zw, r0.xy, r3.xy
sample_l_indexable(texture2d)(float,float,float,float) r3, r3.xyxx, t0, s0, l(0)
add r2, r2, r3
mul r1, r2, l(0.5, 0.5, 0.5, 0.5)
break
case l(2)
sample_l_indexable(texture2d)(float,float,float,float) r2, r0.zwzz, t0, s0, l(0)
mov r3.x, l(0)
mul r3.y, cb0[0].w, l(0.5)
mad r3.xy, cb0[0].zw, r0.xy, r3.xy
sample_l_indexable(texture2d)(float,float,float,float) r3, r3.xyxx, t0, s0, l(0)
add r2, r2, r3
mul r1, r2, l(0.5, 0.5, 0.5, 0.5)
break
default
sample_l_indexable(texture2d)(float,float,float,float) r2, r0.zwzz, t0, s0, l(0)
mul r3.xyz, cb0[0].zwz, l(0.5, 0.5, 0.5, 0)
mov r3.w, l(0)
mad r3, cb0[0].zwzw, r0.xyxy, r3.zwxy
sample_l_indexable(texture2d)(float,float,float,float) r4, r3.xyxx, t0, s0, l(0)
add r2, r2, r4
mov r3.x, l(0)
mul r3.y, cb0[0].w, l(0.5)
mad r0.xy, cb0[0].zw, r0.xy, r3.xy
sample_l_indexable(texture2d)(float,float,float,float) r0, r0.xyxx, t0, s0, l(0)
add r0, r0, r2
sample_l_indexable(texture2d)(float,float,float,float) r2, r3.zwzz, t0, s0, l(0)
add r0, r0, r2
mul r1, r0, l(0.25, 0.25, 0.25, 0.25)
break
endswitch
store_uav_typed u0, vThreadID.xyyy, r1
ret
