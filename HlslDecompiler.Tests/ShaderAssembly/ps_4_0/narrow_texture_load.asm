ps_4_0
dcl_resource_texture2d (uint,uint,uint,uint) t0
dcl_resource_texture2dms(4) (float,float,float,float) t1
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_output o0
dcl_temps 3
ftoi r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
ldms r1, r0.xyww, t1, l(0)
ldms r2, r0.xyww, t1, l(1)
add r1, r1.zyxw, r2.zyxw
ldms r2, r0.xyww, t1, l(2)
add r1, r1, r2.zyxw
ldms r2, r0, t1, l(3)
ld r0, r0.xyww, t0
and r0.x, r0.y, l(128)
add r1, r1, r2.zyxw
mul r1, r1, l(0.25, 0.25, 0.25, 0.25)
movc o0, r0.x, r1, r1.zyxw
ret
