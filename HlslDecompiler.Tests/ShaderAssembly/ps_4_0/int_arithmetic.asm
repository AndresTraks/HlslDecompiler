ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 1
imad r0.x, l(3), cb0[0].x, l(-7)
itof r0.x, r0.x
mul o0, r0.x, v0
ret
