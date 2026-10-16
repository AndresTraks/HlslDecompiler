ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2d (float,float,float,float) t0
dcl_uav_typed_texture2d (uint,uint,uint,uint) u1
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_output o0
dcl_temps 2
ftou r0.xy, v0.yx
ld_uav_typed_indexable(texture2d)(uint,uint,uint,uint) r1.x, r0.y, u1.x
mov o0.x, r1.x
atomic_umax u1, r0.xyxx, l(5)
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r0.xyz, r0.xyz, t0.xyz
mov o0.yzw, r0.xyz
ret
