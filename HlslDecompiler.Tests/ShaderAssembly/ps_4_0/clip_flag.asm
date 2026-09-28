ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
discard_z cb0[0].x
mov o0, v0.wzyx
ret
