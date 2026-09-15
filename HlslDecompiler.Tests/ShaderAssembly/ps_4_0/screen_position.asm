ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_sv linear noperspective v0.xy
dcl_input_ps linear v1.x
dcl_output o0
dcl_temps 4
div r0.yz, v0.xy, cb0[0].xy
mul r1.x, r0.y, l(8)
frc r1.x, r1.x
lt r1.x, r1.x, l(0.5)
and r1.x, r1.x, l(1)
ftoi r2.xy, v0.xy
mov r2.zw, l(0, 0, 0, 0)
iadd r3, r2, l(1, 0, 0, 0)
ld r2, r2.xyww, t0
ld r3, r3, t1
mov r0.x, r3.x
mov r0.w, v1.x
mad o0, r2, r1.x, r0
ret
