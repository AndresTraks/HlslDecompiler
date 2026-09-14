ps_4_0
dcl_resource_buffer (float,float,float,float) t0
dcl_resource_buffer (uint,uint,uint,uint) t1
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 1
mul r0.x, v0.x, l(16)
ftoi r0.x, r0.x
ld r0, r0.x, t1
ld r0, r0.x, t0
mul o0, r0, v0.y
ret
