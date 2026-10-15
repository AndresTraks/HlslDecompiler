cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_typed_texture2d (unorm,unorm,unorm,unorm) u0
dcl_uav_typed_buffer (sint,sint,sint,sint) u1
dcl_input vThreadID.xy
dcl_temps 3
dcl_thread_group 8, 8, 1
ld_uav_typed_indexable(texture2d)(unorm,unorm,unorm,unorm) r0, vThreadID.xyyy, u0
ld_uav_typed_indexable(buffer)(sint,sint,sint,sint) r1.xy, vThreadID.xx, u1.xy
and r2, r1.xyxy, l(1065353216, 1065353216, 1065353216, 1065353216)
itof r1.x, r1.x
mul r1, r0, r1.x
lt r0.x, l(0.5), r0.x
movc r1, r0.x, r2, r1
store_uav_typed u0, vThreadID.xyyy, r1
ftoi r1.y, r0.z
mov r1.xzw, r0.yyy
store_uav_typed u1, vThreadID.x, r1
ret
