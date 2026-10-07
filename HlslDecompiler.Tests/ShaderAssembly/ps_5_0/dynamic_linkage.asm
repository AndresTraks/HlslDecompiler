ps_5_0
dcl_globalFlags refactoringAllowed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_body fb2
dcl_function_body fb3
dcl_function_table ft0 = {fb0}
dcl_function_table ft1 = {fb1}
dcl_function_table ft2 = {fb2}
dcl_function_table ft3 = {fb3}
dcl_interface fp0[1][1] = {ft0, ft1}
dcl_interface fp1[3][1] = {ft2, ft3}
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
fcall fp0[0][0]
fcall fp1[2][0]
add o0, r0, r1
ret
label fb0
add r0, v0, l(-0.125, -0.125, -0.125, -0.125)
ret
label fb1
add r0, v0, l(0.25, 0.25, 0.25, 0.25)
ret
label fb2
mul_sat r1, v0, l(1.5, 1.5, 1.5, 1.5)
ret
label fb3
mul r1, v0, l(0.5, 0.5, 0.5, 0.5)
ret
