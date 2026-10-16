ps_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_typed_texture2d (uint,uint,uint,uint) u1
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_output o0
dcl_temps 1
ftou r0.xy, v0.yx
ld_uav_typed_indexable(texture2d)(uint,uint,uint,uint) r0.z, r0.x, u1.x
atomic_umax u1, r0.xyxx, l(5)
utof o0.y, r0.z
mov o0.x, r0.z
mov o0.zw, l(0, 0, 0, 1)
ret
