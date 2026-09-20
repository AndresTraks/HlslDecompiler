ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
switch cb0[0].x
case l(0)
ilt r1.x, l(1), cb0[0].y
if_nz r1.x
sample r0, v0.xyxx, t0, s0
else
sample r1, v0.yxyy, t0, s0
mul r0, r1, l(3, 3, 3, 3)
endif
break
case l(2)
add r0, v0, v0
break
default
add r0, v0, l(1, 1, 1, 1)
break
endswitch
mov o0, r0
ret
