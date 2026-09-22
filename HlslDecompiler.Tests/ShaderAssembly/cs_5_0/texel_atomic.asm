cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_typed_texture2d (uint,uint,uint,uint) u0
dcl_uav_structured u1, 4
dcl_input vThreadID.xy
dcl_temps 2
dcl_thread_group 8, 8, 1
and r0, vThreadID.xyyy, l(3, 3, 3, 3)
atomic_iadd u0, r0.xwxx, l(1)
imm_atomic_umax r1.x, u0, r0.xwxx, vThreadID.x
store_structured u1.x, vThreadID.x, l(0), r1.x
store_uav_typed u0, r0, r1.x
ret
