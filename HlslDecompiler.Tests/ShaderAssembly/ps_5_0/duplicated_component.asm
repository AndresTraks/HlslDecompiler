ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 4
dcl_input_ps_siv linear noperspective v0, position
dcl_input_ps linear v1
dcl_output o0
dcl_temps 1
ftoi r0.x, v0.y
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, r0.x, l(0), t0.x
and r0.x, r0.x, l(3)
movc r0.y, r0.x, l(0), l(1)
dp4 r0.x, v0, v1
mov o0, r0.xxyy
ret
