ps_5_0
dcl_globalFlags refactoringAllowed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_table ft0 = {fb0}
dcl_function_table ft1 = {fb1}
dcl_interface fp0[1][1] = {ft0, ft1}
dcl_input_ps linear v0
dcl_output o0
dcl_temps 4
fcall fp0[0][0]
add o0, r0, r0
ret
label fb0
lt r1.x, l(0.5), v0.x
mul r2, v0, v0
sqrt r3, |v0|
movc r0, r1.x, r2, r3
ret
label fb1
add_sat r1, v0, v0
mul r2, v0, l(0.5, 0.5, 0.5, 0.5)
mad r0, r1, r1, r2
ret
