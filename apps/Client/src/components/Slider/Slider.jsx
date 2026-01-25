import Box from '@mui/material/Box';
import Slider from '@mui/material/Slider';

function valuetext(value) {
  return `${value}`;
}

function RangeSlider({ text, value, onChange, min, max }) {

  const handleChange = (event, newValue) => {
    if (onChange) {
      onChange(newValue);
    }
  };

  return (
    <Box sx={{ width: '100%' }}>
      <p>{text}</p>
      <Slider
          getAriaLabel={() => text}
          value={value || [0, 100]}
          onChange={handleChange}
          valueLabelDisplay="auto"
          getAriaValueText={valuetext}
          min={min || 0}
          max={max || 100}
      />
    </Box>
  );
}

export default RangeSlider;